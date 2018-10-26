﻿using System;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Tababular;

using Modix.Data.Models;
using Modix.Data.Models.Moderation;
using Modix.Services.Moderation;
using Modix.Services.Core;

namespace Modix.Modules
{
    [Group("infraction"), Alias("infractions")]
    [Summary("Provides commands for working with infractions.")]
    public class InfractionModule : ModuleBase
    {
        public InfractionModule(IModerationService moderationService, IUserService userService)
        {
            ModerationService = moderationService;
            UserService = userService;
        }

        [Command("search")]
        [Summary("Display all infractions for a user, that haven't been deleted.")]
        [Priority(10)]
        public async Task Search(
            [Summary("The user whose infractions are to be displayed.")]
            IGuildUser subject)
        {
            var requestor = Context.User.Mention;
            var subject = await UserService.GetGuildUserSummaryAsync(Context.Guild.Id, subjectId);

            var infractions = await ModerationService.SearchInfractionsAsync(
                new InfractionSearchCriteria
                {
                    GuildId = Context.Guild.Id,
                    SubjectId = subjectId,
                    IsDeleted = false,
                    IsRescinded = false
                },
                new[]
                {
                    new SortingCriteria { PropertyName = "CreateAction.Created", Direction = SortDirection.Descending }
                });

            if (infractions.Count == 0)
            {
                await ReplyAsync(Format.Code("No infractions"));
                return;
            }

            var infractionQuery = infractions.Select(infraction => new
            {
                Id = infraction.Id,
                Created = infraction.CreateAction.Created.ToUniversalTime().ToString("yyyy MMM dd"),
                Type = infraction.Type.ToString(),
                Subject = infraction.Subject.Username,
                Creator = infraction.CreateAction.CreatedBy.DisplayName,
                Reason = infraction.Reason
            }).OrderBy(s => s.Type);

            var noticeCount = infractions.Count(x => x.Type == InfractionType.Notice);
            var warningCount = infractions.Count(x => x.Type == InfractionType.Warning);
            var muteCount = infractions.Count(x => x.Type == InfractionType.Mute);
            var banCount = infractions.Count(x => x.Type == InfractionType.Ban);

            var builder = new EmbedBuilder()
                .WithTitle($"Infractions for user: {subject.Username}#{subject.Discriminator}")
                .WithDescription(
                    $"This user has {noticeCount} notice(s), {warningCount} warning(s), {muteCount} mute(s), and {banCount} ban(s)")
                .WithUrl($"https://mod.gg/infractions/?subject={subject.UserId}")
                .WithColor(new Color(0xA3BF0B))
                .WithTimestamp(DateTimeOffset.Now);

            foreach (var infraction in infractionQuery)
            {
                builder.AddField(
                    $"#{infraction.Id} - {infraction.Type} - Created: {infraction.Created}",
                    $"[Reason: {infraction.Reason}](https://mod.gg/infractions/?id={infraction.Id})"
                );
            }

            var embed = builder.Build();

            await Context.Channel.SendMessageAsync(
                    $"Requested by {requestor}",
                    embed: embed)
                .ConfigureAwait(false);
        }


        [Command("search embed")]
        [Summary("Display all infractions for a user, that haven't been deleted.")]
        public async Task SearchEmbed(
            [Summary("The user whose infractions are to be displayed.")]
            IGuildUser subject)
        {
            await SearchEmbed(subject.Id);
        }

        [Command("delete")]
        [Summary("Marks an infraction as deleted, so it no longer appears within infraction search results")]
        public Task Delete(
            [Summary("The ID value of the infraction to be deleted.")]
                long infractionId)
            => ModerationService.DeleteInfractionAsync(infractionId);

        internal protected IModerationService ModerationService { get; }
        public IUserService UserService { get; }
    }
}
