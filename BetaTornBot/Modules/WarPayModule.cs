using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarPay;

namespace BetaTornBot.Modules
{
    public class WarPayModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("warpay", "Gets the warpay")]
        public async Task calc_war_pay([Summary("key", "Must be a full access apiKey")] string ApiKey, 
           [Summary("cache-value", "Total value of caches in billions")] double totalCacheValue,
           [Summary("outside-hit-threshold", "How big the chain needs to be for outside hits to count")] int outsideChainThreshold = 50,
           [Summary("outside-hit-score", "Score given for an outside hit above threshold")] double outsideHitScore = 3,
           [Summary("bonus-hit-score", "score given for a bonus(10, 25, 50...)")] double bonusHitScore = 10)
        {
            await DeferAsync();   // acknowledges within 3 seconds; Discord shows "Bot is thinking..."

            var result = await WarPayCalculator.CalculateWarPayAsync(40, 3, 10, ApiKey, (long)totalCacheValue * 1000000000);

            await RespondAsync(CreateTable(result.Players));
        }

        private static string CreateTable(IReadOnlyList<PlayerWarPay> players)
        {
            var sb = new StringBuilder();

            sb.AppendLine("```");
            sb.AppendLine("# War Pay Results\n:");

            sb.AppendLine($"{"Name",-15} {"Score",5} {"Pay",8}");
            sb.AppendLine(new string('-', 30));

            foreach (var player in players)
            {
                sb.AppendLine(
                    $"{player.Name,-15} {player.Score,5} {player.Payout,8}");
            }

            return sb.ToString();
        }
    }
}
