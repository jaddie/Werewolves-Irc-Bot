using System;
using System.Collections;
using System.Collections.Generic;
using Squishy.Network;
using WCell.Util.Strings;
using StringStream = WCell.Util.Strings.StringStream;

// for StringStream

namespace Squishy.Irc
{
	public enum Privilege
	{
		Regular = 0,
		Voice = '+',
		HalfOp = '%',
		Op = '@',
		Admin = '&',
		Owner = '~'
	}

	public interface IIrcChannelArgs
	{
	}

	public class IrcChannel : IComparable, ChatTarget
	{
		public readonly IDictionary<string, BanEntry> BanMasks;
		public readonly IDictionary<string, UnbanTimer> UnbanTimers;
		public readonly IDictionary<string, IrcUser> Users;
		public readonly IDictionary<IrcUser, UserPrivSet> Privileges;

		private readonly IrcClient m_irc;
		private readonly string m_name;
		private DateTime m_creationTime;
		private string m_key;
		private int m_limit;
		private string m_modes;
		private string m_topic;
		private IrcUser m_topicSetter;
		private DateTime m_topicSetTime;

		public IrcChannel(IrcClient irc, string name)
		{
			m_irc = irc;
			m_name = name;

			Users = new Dictionary<string, IrcUser>(StringComparer.InvariantCultureIgnoreCase);
			Privileges = new Dictionary<IrcUser, UserPrivSet>();
			BanMasks = new Dictionary<string, BanEntry>(StringComparer.InvariantCultureIgnoreCase);
			UnbanTimers = new Dictionary<string, UnbanTimer>(StringComparer.InvariantCultureIgnoreCase);
			m_topic = "";
			ClearModes();
		}

		#region Props / Info

		/// <summary>
		/// The IrcClient which this channel belongs to.
		/// </summary>
		public IrcClient IrcClient
		{
			get { return m_irc; }
		}

		/// <summary>
		/// The name of this Channel (including its prefix).
		/// </summary>
		public string Name
		{
			get { return m_name; }
		}

		/// <summary>
		/// The current topic of the Channel.
		/// </summary>
		public string Topic
		{
			get { return m_topic; }
			set { m_irc.CommandHandler.SetTopic(m_name, value); }
		}

		/// <summary>
		/// The modes that are currently set on this channel.
		/// </summary>
		public string Modes
		{
			get { return m_modes; }
		}

		/// <summary>
		/// The DateTime instance which represents the time when this Channel has been created.
		/// </summary>
		public DateTime CreationTime
		{
			get { return m_creationTime; }
		}

		/// <summary>
		/// Returns the current key of the Channel. If there is no key, "" will be returned.
		/// </summary>
		public string Key
		{
			get { return m_key; }
		}

		/// <summary>
		/// Returns the current limit on the Channel. If there is no limit, 0 will be returned.
		/// </summary>
		public int Limit
		{
			get { return m_limit; }
		}

		/// <summary>
		/// Will be set after joining a channel. 
		/// Use the UsersAdded event to make sure that this is set.
		/// </summary>
		public IrcUser TopicSetter
		{
			get { return m_topicSetter; }
			internal set { m_topicSetter = value; }
		}

		/// <summary>
		/// Will be set after joining a channel. 
		/// Use the UsersAdded event to make sure that this is set.
		/// </summary>
		public DateTime TopicSetTime
		{
			get { return m_topicSetTime; }
			internal set { m_topicSetTime = value; }
		}

		/// <summary>
		/// Returns a User within this channel with the corresponding nick.
		/// </summary>
		public IrcUser this[string nick]
		{
			get { return GetUser(nick); }
		}

		public void Msg(object format, params object[] args)
		{
			m_irc.CommandHandler.Msg(this, format, args);
		}

		public void Notice(string line)
		{
			m_irc.CommandHandler.Notice(this, line);
		}

		public string Identifier
		{
			get { return Name; }
		}

		/// <summary>
		/// Compares this channel'str name with the specified string case-insensitively.
		/// </summary>
		public bool Is(string name)
		{
			return name.ToLower() == m_name.ToLower();
		}

		/// <summary>
		/// Returns the User with the specified nick who is in this Channel or null if there is no such User.
		/// </summary>
		public IrcUser GetUser(string nick)
		{
			return Users[nick];
		}

		/// <summary>
		/// Returns wether or not a User with the specified nick is on this Channel.
		/// </summary>
		public bool HasUser(string nick)
		{
			return Users.ContainsKey(nick);
		}

		public Privilege GetHighestPriv(IrcUser user)
		{
			UserPrivSet privs = GetPrivs(user);
			if (privs != null)
			{
				return privs.Highest;
			}
			return Privilege.Regular;
		}

		public UserPrivSet GetPrivs(IrcUser user)
		{
			UserPrivSet set;
			Privileges.TryGetValue(user, out set);
			return set;
		}

		/// <returns>Wether the given user has the given priv on this channel.</returns>
		public bool HasPriv(IrcUser user, Privilege priv)
		{
			UserPrivSet set;
			if (Privileges.TryGetValue(user, out set))
			{
				return set.Has(priv);
			}
			return false;
		}

		/// <returns>Wether the given user has at least the given privs (or has even more privs).</returns>
		public bool IsUserAtLeast(IrcUser user, Privilege priv)
		{
			UserPrivSet set;
			if (Privileges.TryGetValue(user, out set))
			{
				return set.HasAtLeast(priv);
			}
			return false;
		}

		/// <summary>
		/// Returns wether or not this channel has the specified flags, independent on their sequence.
		/// </summary>
		public bool HasMode(string modes)
		{
			foreach (char c in modes)
				if (m_modes.IndexOf(c) < 0)
					return false;
			return true;
		}

		/// <returns>Wether or not if the specific user is op on this channel (Has "@" flag)</returns>
		public bool HasOp(IrcUser user)
		{
			return HasPriv(user, Privilege.Op);
		}

		/// <returns>Wether or not if the user with the specific nick is op on this channel (Has "@" flag)</returns>
		public bool HasOp(string nick)
		{
			return HasPriv(m_irc.GetUser(nick), Privilege.Op);
		}

		/// <returns>Wether or not if the given user is voiced on this channel (Has "+" flag)</returns>
		public bool HasVoice(IrcUser user)
		{
			return HasPriv(user, Privilege.Voice);
		}

		/// <returns>Wether or not if the user with the specific nick is voiced on this channel (Has "+" flag)</returns>
		public bool HasVoice(string nick)
		{
			return HasVoice(m_irc.GetUser(nick));
		}

		public bool IsUser(string nick, Privilege priv)
		{
			return IsUser(m_irc.GetUser(nick), priv);
		}

		/// <returns>Wether the given user exists and has at least the given priv.</returns>
		public bool IsUserAtLeast(string nick, Privilege priv)
		{
			return IsUserAtLeast(m_irc.GetUser(nick), priv);
		}

		/// <returns>Wether the given user exists and has the given priv.</returns>
		public bool IsUser(IrcUser user, Privilege priv)
		{
			return HasPriv(user, priv);
		}

		/// <summary>
		/// Returns the modes that are set on this channel.
		/// </summary>
		public string GetModes()
		{
			return m_modes;
		}

		/// <summary>
		/// Indicates wether or not the specified banmask has been set on this Channel.
		/// </summary>
		public bool IsBanned(string Banmask)
		{
			return BanMasks.ContainsKey(Banmask);
		}

		#endregion

		#region Events

		#region Delegates

		public delegate void BanListCompleteHandler();

		public delegate void BanListEntrySentHandler(BanEntry entry);

		public delegate void ChanCreationTimeSentHandler(DateTime creationTime);

		public delegate void FlagsChangedHandler(IrcUser user, Privilege priv, IrcUser target);

		public delegate void ModeChangedHandler(IrcUser user, string mode, string param);

		public delegate void MsgReceivedHandler(IrcUser user, StringStream text);

		public delegate void NoticeReceivedHandler(IrcUser user, StringStream text);

		public delegate void TextHandler(IrcUser user, StringStream text);

		public delegate void TopicChangedHandler(IrcUser user, string text, bool initial);

		public delegate void UserJoinedHandler(IrcUser user);

		public delegate void UserKickedHandler(IrcUser from, IrcUser target, string reason);

		public delegate void UserLeftHandler(IrcUser user, string reason);

		public delegate void UserPartedHandler(IrcUser user, string reason);

		public delegate void UsersAddedHandler(IrcUser[] users);

		#endregion

		/// <summary>
		/// Fires when there is any kind of PRIVMSG or NOTICE excluding CTCP requests sent to this channel.
		/// </summary>
		/// <param name="user">The User who sent the text</param>
		/// <param name="text">The text which was sent</param>
		public event TextHandler TextReceived;

		internal void TextNotify(IrcUser user, StringStream text)
		{
			if (TextReceived != null)
				TextReceived(user, text);
		}

		/// <summary>
		/// Fires when there is any CTCP ACTION sent to this channel.
		/// </summary>
		/// <param name="user">The User who sent the text</param>
		/// <param name="text">The text which was sent</param>
		public event TextHandler ActionReceived;

		internal void ActionNotify(IrcUser user, StringStream text)
		{
			if (ActionReceived != null)
				ActionReceived(user, text);
		}
		/// <summary>
		/// Fires when the Client receives a PRIVMSG which was directed to this Channel.
		/// </summary>
		public event MsgReceivedHandler MsgReceived;

		internal void MsgReceivedNotify(IrcUser user, StringStream text)
		{
			if (MsgReceived != null)
				MsgReceived(user, text);
		}

		/// <summary>
		/// Fires when the Client receives a PRIVMSG, directed to this Client itself.
		/// </summary>
		public event NoticeReceivedHandler NoticeReceived;

		internal void NoticeReceivedNotify(IrcUser user, StringStream text)
		{
			if (NoticeReceived != null)
				NoticeReceived(user, text);
		}

		/// <summary>
		/// Fires when the Client receives any kind of NOTICE.
		/// </summary>
		/// <param name="user">The User who sent the text</param>
		/// <param name="channel">The Channel where it was sent (can be null)</param>
		/// <param name="text">The text which was sent</param>
		protected virtual void OnNotice(IrcUser user, IrcChannel channel, StringStream text)
		{
		}

		internal void NoticeNotify(IrcUser user, IrcChannel channel, StringStream text)
		{
			OnNotice(user, channel, text);
		}

		/// <summary>
		/// Fires when the specified User joins this Channel.
		/// </summary>
		public event UserJoinedHandler UserJoined;

		internal void UserJoinedNotify(IrcUser user)
		{
			AddUser(user);
			if (UserJoined != null)
				UserJoined(user);
		}

		/// <summary>
		/// Fires when the Client receives the Names list for this Channel.
		/// </summary>
		/// <param name="users">An Array of Users who are on the Channel</param>
		public event UsersAddedHandler UsersAdded;

		internal void UsersAddedNotify(IrcUser[] users)
		{
			if (UsersAdded != null)
			{
				UsersAdded(users);
			}
		}

		/// <summary>
		/// Fires when the Topic for this Channel has been sent.
		/// Either when joining a Channel or modified by a User.
		/// </summary>
		public event TopicChangedHandler TopicChanged;

		internal void TopicChangedNotify(IrcUser user, string text, bool initial)
		{
			if (TopicChanged != null)
				TopicChanged(user, text, initial);
		}

		/// <summary>
		/// Fires when a User adds a Channel mode.
		/// </summary>
		/// <param name="user">The User who has added the mode</param>
		/// <param name="mode">The mode which has been changed</param>
		/// <param name="param">"" if the mode does not have any parameter</param>
		public event ModeChangedHandler ModeAdded;

		internal void ModeAddedNotify(IrcUser user, string mode, string param)
		{
			if (ModeAdded != null)
				ModeAdded(user, mode, param);
		}

		/// <summary>
		/// Fures when a User deletes a Channel mode.
		/// </summary>
		/// <param name="user">The User who has added the mode</param>
		/// <param name="channel">The channel on which the mode has been changed</param>
		/// <param name="mode">The mode which has been changed</param>
		/// <param name="param">"" if the mode does not have any parameter</param>
		public event ModeChangedHandler ModeDeleted;

		internal void ModeDeletedNotify(IrcUser user, string mode, string param)
		{
			if (ModeDeleted != null)
				ModeDeleted(user, mode, param);
		}

		/// <summary>
		/// Fires when a User adds a Channel flag to another User.
		/// </summary>
		public event FlagsChangedHandler FlagAdded;

		internal void FlagAddedNotify(IrcUser user, Privilege priv, IrcUser target)
		{
			if (FlagAdded != null)
				FlagAdded(user, priv, target);
		}

		/// <summary>
		/// Fires when a User deletes a Channel flag from another User.
		/// </summary>
		public event FlagsChangedHandler FlagDeleted;

		internal void FlagDeletedNotify(IrcUser user, Privilege priv, IrcUser target)
		{
			if (FlagDeleted != null)
				FlagDeleted(user, priv, target);
		}

		//public delegate void UserLeftHandler(User user, User target, string reason);

		/// <summary>
		/// Fires when a User is kicked from a Channel.
		/// </summary>
		public event UserKickedHandler UserKicked;

		internal void UserKickedNotify(IrcUser from, IrcUser target, string reason)
		{
			if (UserKicked != null)
				UserKicked(from, target, reason);
			UserLeftNotify(target, reason);
		}

		/// <summary>
		/// Fires when a User parts from a Channel.
		/// </summary>
		public event UserPartedHandler UserParted;

		internal void UserPartedNotify(IrcUser user, string reason)
		{
			if (UserParted != null)
				UserParted(user, reason);
			UserLeftNotify(user, reason);
		}

		/// <summary>
		/// Fires when a user of this channel has left (due to kick, part or quit).
		/// </summary>
		public event UserLeftHandler UserLeft;

		internal void UserLeftNotify(IrcUser user, string reason)
		{
			m_irc.UserLeftChannelNotify(this, user, reason);
			if (UserLeft != null)
				UserLeft(user, reason);
			DeleteUser(user);
		}

		/// <summary>
		/// Fires when the CreationTime of a Channel has been sent (raw 329)
		/// </summary>
		public event ChanCreationTimeSentHandler ChanCreationTimeSent;

		internal virtual void ChanCreationTimeSentNotify(DateTime creationTime)
		{
			if (ChanCreationTimeSent != null)
				ChanCreationTimeSent(creationTime);
		}

		/// <summary>
		/// Fires when an already established BanEntry has been sent (raw 367).
		/// </summary>
		public event BanListEntrySentHandler BanListEntrySent;

		internal void BanListEntrySentNotify(BanEntry entry)
		{
			if (BanListEntrySent != null)
				BanListEntrySent(entry);
		}

		/// <summary>
		/// Fires when an already established BanEntry has been sent (raw 367).
		/// </summary>
		public event BanListCompleteHandler BanListComplete;

		internal void BanListCompleteNotify()
		{
			if (BanListComplete != null)
				BanListComplete();
		}

		#endregion

		#region Handling the Channel

		internal IrcUser[] AddNames(string nickString)
		{
			string[] nicks = nickString.Split(' ');
			var users = new IrcUser[nicks.Length];
			for (int i = 0; i < nicks.Length; i++)
			{
				string nick = nicks[i];
				string c = nick[0].ToString();
				string flags = "";
				if (m_irc.SupportsSymbols(c))
				{
					flags = c;
					nick = nick.Substring(1);
				}

				IrcUser u;
				if ((u = m_irc.GetUser(nick)) == null)
					u = new IrcUser(m_irc, nick, this);
				users[i] = u;
				AddUser(u, flags);
				u.AddChannel(this);
			}
			return users;
		}

		internal void AddUser(IrcUser user)
		{
			string nick = user.Nick;
			if (!Users.ContainsKey(nick))
				Users.Add(nick, user);
			if (!Privileges.ContainsKey(user))
				Privileges.Add(user, new UserPrivSet());
		}

		internal void AddUser(IrcUser user, string flags)
		{
			string nick = user.Nick;
			if (!Users.ContainsKey(nick))
				Users.Add(nick, user);
            if (!Privileges.ContainsKey(user))
                Privileges.Add(user, new UserPrivSet(flags));
            else
                Privileges[user].Set(flags);
		}

		internal void DeleteUser(IrcUser user)
		{
			Users.Remove(user.Nick);
			Privileges.Remove(user);
		}

		internal void SetTopic(string text)
		{
			m_topic = text;
		}

		internal void SetCreationTime(DateTime when)
		{
			m_creationTime = when;
		}

		internal void OnNickChange(IrcUser user, string oldNick)
		{
			Users.Remove(oldNick);
			Users[user.Nick] = user;
		}

		internal void ClearModes()
		{
			m_modes = "";
			m_key = "";
			m_limit = -1;
		}

		internal void AddMode(string mode, string args)
		{
			if (mode == "k")
				m_key = args;
			else if (mode == "l")
				m_limit = Convert.ToInt32(args);
			if (m_modes.IndexOf(mode) == -1 && mode != "b")
				m_modes += mode;
		}

		internal void DeleteMode(string mode)
		{
			if (mode == "k")
				m_key = "";
			else if (mode == "l")
				m_limit = -1;
			if (m_modes.IndexOf(mode) > -1)
				m_modes = m_modes.Remove(m_modes.IndexOf(mode), mode.Length);
		}

		internal void AddFlag(Privilege priv, IrcUser user)
		{
			var uprivs = GetPrivs(user);
			if (uprivs != null)
			{
				uprivs.Add(priv);
			}
		}

		internal void DeleteFlag(Privilege priv, IrcUser user)
		{
			var uprivs = GetPrivs(user);
			if (uprivs != null)
			{
				uprivs.Remove(priv);
			}
		}

		internal void AddUnbanTimer(UnbanTimer timer)
		{
			if (!UnbanTimers.ContainsKey(timer.Mask))
			{
				UnbanTimers.Add(timer.Mask, timer);
			}
		}

		internal void ElapsUnbanTimer(UnbanTimer timer)
		{
			if (UnbanTimers.ContainsKey(timer.Mask))
			{
				UnbanTimers.Remove(timer.Mask);
				m_irc.CommandHandler.Unban(this, timer.Mask);
			}
		}

		#endregion

		/// <summary>
		/// Custom data associated with this IrcChannel.
		/// </summary>
		public IIrcChannelArgs Args
		{
			get;
			set;
		}

		#region IComparable Members

		public int CompareTo(object channel)
		{
			var c = (IrcChannel)channel;
			return m_name.CompareTo(c.Name);
		}

		#endregion

		public override string ToString()
		{
			return m_name;
		}

		public IEnumerator GetEnumerator()
		{
			return Users.Values.GetEnumerator();
		}
	}
}