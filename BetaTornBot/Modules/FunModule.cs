using Discord;
using Discord.Interactions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BetaTornBot.Modules
{
    public class FunModule : InteractionModuleBase<SocketInteractionContext>
    {
        [SlashCommand("sinister-frog", "summons the sinister frog")]
        public async Task SinisterFrog()
        {
            await RespondAsync(
                "https://media.giphy.com/media/3vxRVs41HKfsxXT4ee/giphy.gif"
            );
        }
    }
}
