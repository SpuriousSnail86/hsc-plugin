using System;
using System.Diagnostics;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin;
using Dalamud.Logging;
using Melanchall.DryWetMidi.Interaction;
using HSC.Control.MidiControl;
using HSC.Control.CharacterControl;
using System.Threading.Tasks;
using static HSC.HSC;
using HSC.Managers;

namespace HSC
{
	/// <summary>
	/// author akira045/Ori
	/// </summary>
	public class ChatCommand
	{
		public static bool IgnoreSwitchSongFlag;
		public static bool IgnoreReloadPlaylist;
		public static void OnChatMessage(XivChatType type, uint senderId, ref SeString sender, ref SeString message, ref bool isHandled)
		{
			if (isHandled)
				return;

			if (type != XivChatType.Party)
			{
				return;
			}

			string[] strings = message.ToString().Split(' ');
			if (strings.Length < 1)
			{
				return;
			}

			string cmd = strings[0].ToLower();

			if (cmd == "speed")
            {
				Task.Run(() => MidiPlayerControl.SetSpeed(float.Parse(strings[1])));
			}

			if (cmd == "seek")
			{
				var dt = DateTime.Parse(strings[1]);
				Task.Run(() => MidiPlayerControl.ChangeTime(dt.Hour, dt.Minute, dt.Second));
			}

			if (cmd == "rewind")
			{
				double timeInSeconds = -5;
				try
				{
					timeInSeconds = -double.Parse(strings[1]);
				}
				catch (Exception e)
				{
				}

				MidiPlayerControl.MoveTime(timeInSeconds);
			}

			if (cmd == "fastforward")
			{

				double timeInSeconds = 5;
				try
				{
					timeInSeconds = double.Parse(strings[1]);
				}
				catch (Exception e)
				{
				}

				MidiPlayerControl.MoveTime(timeInSeconds);
			}
		}
	}
}