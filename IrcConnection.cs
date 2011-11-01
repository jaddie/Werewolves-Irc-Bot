using System;
using System.Linq;
using Squishy.Irc;
using Squishy.Irc.Commands;

namespace WerewolvesBot
{
    public class IrcConnection : IrcClient
    {
        public IrcConnection()
        {
            protHandler.PacketReceived += protHandler_PacketReceived;
        }

        void protHandler_PacketReceived(Squishy.Irc.Protocol.IrcPacket packet)
        {
            Console.WriteLine(packet);
        }

        public override bool MayTriggerCommand(WCell.Util.Commands.CmdTrigger<IrcCmdArgs> trigger, WCell.Util.Commands.Command<IrcCmdArgs> cmd)
        {
            return true;
        }
        protected override void OnBeforeSend(string text)
        {
            Console.WriteLine(text);
        }

        protected override void Perform()
        {
            base.Perform();
            foreach (string chan in Properties.Settings.Default.IrcChannels)
            {
                if(chan.Contains(','))
                {
                    string[] chaninfo = chan.Split(new []{','},StringSplitOptions.RemoveEmptyEntries);
                    CommandHandler.Join(chaninfo[0], chaninfo[1]);
                        //TODO: Logging channel join
                }
            }
        }
    }
}
