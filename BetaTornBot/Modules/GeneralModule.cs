using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Text;

namespace BetaTornBot.Modules
{
    public class GeneralModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("ping", "TestMethod")]
        public async Task Ping()
        {
            await RespondAsync($"Pong! {Context.Client.CurrentUser.GlobalName}");
        }
    }
}
