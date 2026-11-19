using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WarPay;

namespace BetaTornBot.Modules
{
    public class WarPayModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("discordwarpay", "Gets the warpay formatted for discord")]
        public async Task GetWarPayDiscord([Summary("key", "Must be a full access apiKey")] string ApiKey,
        [Summary("cache-value", "Total value of caches in billions. If you want to be more exact remember to use . DOT for decimals")] double totalCacheValue,
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

                // One message normally; several if the table is too long for Discord's 2000 character limit.
                foreach (var message in WarPayFormatter.ToDiscordMessages(result, "War Pay Results"))
                    await FollowupAsync(message);
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




        [SlashCommand("htmlwarpay", "Gets the warpay formatted for html")]
        public async Task GetWarPayHTML([Summary("key", "Must be a full access apiKey")] string ApiKey,
        [Summary("cache-value", "Total value of caches in billions. If you want to be more exact remember to use . DOT for decimals")] double totalCacheValue,
        [Summary("outside-hit-threshold", "How big the chain needs to be for outside hits to count")] int outsideChainThreshold = 50,
        [Summary("outside-hit-score", "Score given for an outside hit above threshold")] double outsideHitScore = 3,
        [Summary("bonus-hit-score", "score given for a bonus(10, 25, 50...)")] double bonusHitScore = 10,
        [Summary("faction-take", "Percentage of war pay that goes to the faction in percentage (20 = 20%)")] double factionTake = 20,
        [Summary("FullStats", "if false returns only the table of warpayments")] bool fullstats = false)
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

                NumberFormatInfo inv = new()
                {
                    NumberDecimalDigits = 0
                };


                var payoutPool = (long)(totalCacheValue * 1_000_000_000 * (1 - (factionTake / 100)));


                StringBuilder sb = new StringBuilder();
                sb.Append("<html>");
                    sb.Append("<head>");
                    //head content here
                        sb.Append("<style>");
                            sb.Append(HTMLHelpers.HtmlStyles.WarPayTableStyles());
                        sb.Append("</style>");
                    sb.Append("</head>");
                    sb.Append("<body>");
                //stats section

                if (fullstats) 
                {
                    sb.Append("<p>Total Cache Value: " + totalCacheValue + "</p>");
                    sb.Append("<p>Faction Take: " + factionTake + "%</p>");
                    sb.Append("<p>Total member payout: " + payoutPool + "</p>");
                }
                        sb.Append("<h1>War Pay Results</h1>");


                        sb.Append("<br />");
                        //payout table
                        sb.Append("<table>"); 
                            sb.Append("<tr>");
                                sb.Append("<th>Name</th>");
                                sb.Append("<th>Score</th>");
                                sb.Append("<th>Pay</th>"); 
                            sb.Append("</tr>");
                            foreach(var player in result.Players)
                            {
                                sb.Append("<tr>");
                                    sb.Append("<td>" + player.Name + "</td>");
                                    sb.Append("<td>" + player.Score.ToString("N0", inv) + "</td>");
                                    sb.Append("<td>" + player.Payout.ToString("N0", inv) + "</td>");
                                sb.Append("</tr>");
                            }
                        sb.Append("</table>");
                    sb.Append("</body>");
                sb.Append("</html>");

                await FollowupAsync(sb.ToString());

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
    }
}
