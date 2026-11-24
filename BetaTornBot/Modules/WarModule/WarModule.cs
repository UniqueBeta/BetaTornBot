using BetaTornBot.Modules.WarModule.WarCalcs.UpcomingTargets;
using BetaTornBot.Modules.WarModule.WarCalcs.WarPerformance;
using BetaTornBot.Shared;
using BetaTornBot.Shared.Formatting;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarPay;
namespace BetaTornBot.Modules.WarModule
{
    public enum CommandTarget
    {
        Discord,
        Html
    }

    public class WarPayModule : InteractionModuleBase<SocketInteractionContext>
    {
        private readonly string ApiKey = Environment.GetEnvironmentVariable("ADMIN_KEY")!;
        private static readonly HttpClient SharedHttp = new();

        [SlashCommand("warpay", "Gets the warpay formatted for discord")]
        public async Task GetWarPay(
        [Summary("cache-value", "Total value of caches in billions. If you want to be more exact remember to use . DOT for decimals")] double totalCacheValue,
        [Summary("output-target", "Whether to output for html or discord")] CommandTarget target,
        [Summary("outside-hit-threshold", "How big the chain needs to be for outside hits to count")] int outsideChainThreshold = 50,
        [Summary("outside-hit-score", "Score given for an outside hit above threshold")] double outsideHitScore = 3,
        [Summary("bonus-hit-score", "score given for a bonus(10, 25, 50...)")] double bonusHitScore = 10,
        [Summary("faction-take", "Percentage of war pay that goes to the faction in percentage (20 = 20%)")] double factionTake = 20)
        {
            // Discord only allows 3 seconds for the first reply, and fetching a war from Torn can take longer.
            // Acknowledge now, then send the table with FollowupAsync once it is ready.
            await DeferAsync();

            try
            {
                var result = await WarPayCalculator.CalculateWarPayAsync(
                    outsideChainThreshold,
                    outsideHitScore,
                    bonusHitScore,
                    ApiKey,
                    totalCacheValue,
                    factionTake);

                if (target == CommandTarget.Discord)
                {
                    // One message normally; several if the table is too long for Discord's 2000 character limit.
                    foreach (var message in WarPayFormatter.ToDiscordMessages(result, "War Pay Results"))
                        await FollowupAsync(message);
                    return;
                }
                else
                {
                    NumberFormatInfo inv = new()
                    {
                        NumberDecimalDigits = 0
                    };

                    var html = WarPayHtml.Build(result, (decimal)totalCacheValue * 1_000_000_000, (decimal)factionTake, true);
                    if(html.Count < 1)
                    {
                        await FollowupAsync("This really should happen, alert beta");
                    }
                    foreach (var h in html)
                    {
                        await FollowupAsync(h);
                    }
                    return;
                }
            }
            catch (TornApiException ex)
            {
                await FollowupAsync($"Torn API error: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                await FollowupAsync($"Invalid input: {ex.Message}");
            }
        }


        [SlashCommand("get-upcoming", "gets upcoming targets for war")]
        public async Task GetUpcomingWarTargets([Summary("OutputTarget", "target destination for output")]CommandTarget target, [Summary("limit", "how many targets should be shown")]int limit = 10)
        {
            await DeferAsync();
            try
            {
                var service = new TornFactionService(new HttpClient(), Environment.GetEnvironmentVariable("PUBLIC_TORN_TOKEN"));
                List<string> lines;
                StringBuilder sb = new StringBuilder();
                if (target == CommandTarget.Discord)
                {
                    Console.WriteLine("Getting targets as discord");
                    lines = await service.GetEnemyMembersByTimeOkayAsDiscordMemberAsync();
                }
                else
                {
                    Console.WriteLine("Getting targets as html");
                    lines = await service.GetEnemyMembersByTimeUntilOkAsHtmlAsync();
                }




                //all these reversals need to be fixed, but fixing the fuckup causing them seems like work
                if (limit < lines.Count)
                {
                    var newL = lines.Slice(0, limit);
                    newL.Reverse();
                    foreach (var line in newL)
                    {
                        //this works
                        sb.AppendLine(line);
                    }
                }
                else
                {
                    lines.Reverse();
                    foreach (var line in lines)
                    {
                        //this works
                        sb.AppendLine(line);
                    }
                }

                await FollowupAsync(sb.ToString());
            }
            catch(Exception ex)
            {
                await FollowupAsync($"Beta fucked this up: " + ex.Message);
            }
        }



        [SlashCommand("wardefense", "How many times each of our members was hit by the enemy in the current war")]
        public async Task war_defense([Summary("warId", "Id if the war is not currently ongoing")] long? warId = null)
        {
            await RunDeferredAsync(async () =>
            {
                var result = await WarDefenseCalculator.CountTimesHitAsync(ApiKey, warId);

                foreach (var message in WarDefenseFormatter.ToDiscordMessages(result))
                    await FollowupAsync(message);
            });
        }

        [SlashCommand("enemy-respect-earned", "How many times our members were hit in the current war, and the respect the earned from it")]
        public async Task enemy_respect_earned([Summary("warId", "Id if the war is not currently ongoing")] long? warId = null)
        {
            await RunDeferredAsync(async () =>
            {
                var result = await WarDefenseCalculator.CountTimesHitWithRespectAsync(ApiKey, warId);

                foreach (var message in RespectEarnedFormatter.ToDiscordMessages(result))
                    await FollowupAsync(message);
            });
        }

        [SlashCommand("warperformance", "Per member: respect gained minus respect the enemy gained from hitting them, current war")]
        public async Task war_performance([Summary("warId", "Id if the war is not currently ongoing")] long? warId = null)
        {
            await RunDeferredAsync(async () =>
            {
                var result = await WarPerformanceCalculator.CalculatePerformanceAsync(ApiKey, warId);

                foreach (var message in WarPerformanceFormatter.ToDiscordMessages(result))
                    await FollowupAsync(message);
            });
        }

        [SlashCommand("formerwars", "Lists former wars as: TheirName vs OurName: war id")]
        public async Task former_wars(
        [Summary("count", "How many wars to list, newest first")] int count = 10)
        {
            await RunDeferredAsync(async () =>
            {
                var wars = await WarListCalculator.GetFormerWarsAsync(ApiKey, count);

                foreach (var message in FormerWarsFormatter.ToDiscordMessages(wars))
                    await FollowupAsync(message);
            });
        }
        // Discord only allows 3 seconds for a first reply and Torn can be slower than that, so every command
        // acknowledges first and then sends its result as a follow-up. Known failures become a short reply.
        private async Task RunDeferredAsync(Func<Task> work)
        {
            await DeferAsync();

            try
            {
                await work();
            }
            catch (TornApiException ex)
            {
                await FollowupAsync($"Torn API error: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                await FollowupAsync($"Invalid input: {ex.Message}");
            }
            catch (HttpRequestException)
            {
                await FollowupAsync("Couldn't reach Torn. Try again in a moment.");
            }
        }

    }
}
