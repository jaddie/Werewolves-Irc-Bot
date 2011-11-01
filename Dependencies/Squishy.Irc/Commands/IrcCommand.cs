using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WCell.Util.Commands;

namespace Squishy.Irc.Commands
{
	public abstract class IrcCommand : Command<IrcCmdArgs>
	{

		public abstract new class SubCommand : BaseCommand<IrcCmdArgs>.SubCommand
		{
		}
	}
}
