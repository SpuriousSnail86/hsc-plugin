﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Game.Internal.Network;
using Dalamud.Plugin;
using Melanchall.DryWetMidi.Core;
using Melanchall.DryWetMidi.Devices;
using Melanchall.DryWetMidi.Interaction;
using MidiBard.Attributes;
using playlibnamespace;

namespace MidiBard
{
	public class Plugin : IDalamudPlugin
	{
		internal static DalamudPluginInterface pluginInterface;
		internal static PluginCommandManager<Plugin> commandManager;
		internal static Configuration config;
		internal static PluginUI ui;
		internal static BardPlayDevice BardPlayer;

		internal static Playback currentPlayback;
		internal static MidiFile CurrentFile;
		internal static TempoMap CurrentTMap;
		internal static List<(TrackChunk, string)> CurrentTracks;
		internal static Localizer localizer;
		private static int configSaverTick;

		internal static AgentInterface MetronomeAgent;
		internal static AgentInterface PerformanceAgent;
		internal static bool InPerformanceMode => Marshal.ReadByte(PerformanceAgent.Pointer + 0x20) != 0;
		internal static bool MetronomeRunning => Marshal.ReadByte(MetronomeAgent.Pointer + 0x73) == 1;
		internal static bool EnsembleModeRunning => Marshal.ReadByte(MetronomeAgent.Pointer + 0x80) == 1;
		private static bool wasEnsembleModeRunning = false;
		internal static byte MetronomeBeatsperBar => Marshal.ReadByte(MetronomeAgent.Pointer + 0x72);
		internal static int MetronomeBeatsElapsed => Marshal.ReadInt32(MetronomeAgent.Pointer + 0x78);
		internal static long MetronomeTickRate => Marshal.ReadInt64(MetronomeAgent.Pointer + 0x60);
		internal static long MetronomeTimer1 => Marshal.ReadInt64(MetronomeAgent.Pointer + 0x48);
		internal static long MetronomeTimer2 => Marshal.ReadInt64(MetronomeAgent.Pointer + 0x50);

		internal static bool notePressed => Marshal.ReadByte(PerformanceAgent.Pointer + 0x60) != 0x9C;
		internal static byte noteNumber => notePressed ? Marshal.ReadByte(PerformanceAgent.Pointer + 0x60) : (byte)0;
		internal static long PerformanceTimer1 => Marshal.ReadInt64(PerformanceAgent.Pointer + 0x38);
		internal static long PerformanceTimer2 => Marshal.ReadInt64(PerformanceAgent.Pointer + 0x40);

		internal static bool IsPlaying => currentPlayback?.IsRunning == true;

		public string Name => "MidiBard";

		internal static readonly ReadingSettings readingSettings = new ReadingSettings
		{
			NoHeaderChunkPolicy = NoHeaderChunkPolicy.Ignore,
			NotEnoughBytesPolicy = NotEnoughBytesPolicy.Ignore,
			InvalidChannelEventParameterValuePolicy = InvalidChannelEventParameterValuePolicy.ReadValid,
			InvalidChunkSizePolicy = InvalidChunkSizePolicy.Ignore,
			InvalidMetaEventParameterValuePolicy = InvalidMetaEventParameterValuePolicy.SnapToLimits,
			MissedEndOfTrackPolicy = MissedEndOfTrackPolicy.Ignore,
			UnexpectedTrackChunksCountPolicy = UnexpectedTrackChunksCountPolicy.Ignore,
			ExtraTrackChunkPolicy = ExtraTrackChunkPolicy.Read,
			UnknownChunkIdPolicy = UnknownChunkIdPolicy.ReadAsUnknownChunk,
			SilentNoteOnPolicy = SilentNoteOnPolicy.NoteOff,
			TextEncoding = Encoding.Default,
			InvalidSystemCommonEventParameterValuePolicy = InvalidSystemCommonEventParameterValuePolicy.SnapToLimits
		};

		public void Initialize(DalamudPluginInterface pi)
		{

			pluginInterface = pi;
			config = (Configuration)pluginInterface.GetPluginConfig() ?? new Configuration();
			config.Initialize(pluginInterface);

			localizer = new Localizer((UILang)config.uiLang);

			ui = new PluginUI();
			pluginInterface.UiBuilder.OnBuildUi += ui.Draw;
			commandManager = new PluginCommandManager<Plugin>(this, pluginInterface);

			playlib.initialize(pluginInterface);
			BardPlayer = new BardPlayDevice();

			AgentManager.Initialize();

			MetronomeAgent = AgentManager.FindAgentInterfaceByVtable(pi.TargetModuleScanner.GetStaticAddressFromSig("48 8D 05 ?? ?? ?? ?? 48 89 03 48 8D 4B 40"));
			PerformanceAgent = AgentManager.FindAgentInterfaceByVtable(pi.TargetModuleScanner.GetStaticAddressFromSig("48 8D 05 ?? ?? ?? ?? 48 8B F9 48 89 01 48 8D 05 ?? ?? ?? ?? 48 89 41 28 48 8B 49 48"));


			foreach (var fileName in config.Playlist)
			{
				PluginLog.LogDebug($"-> {fileName} START");

				try
				{
					//_texToolsImport = new TexToolsImport(new DirectoryInfo(_base._plugin!.Configuration!.CurrentCollection));
					//_texToolsImport.ImportModPack(new FileInfo(fileName));

					using (var f = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
					{
						var loaded = MidiFile.Read(f, readingSettings);
						//PluginLog.Log(f.Name);
						//PluginLog.LogDebug($"{loaded.OriginalFormat}, {loaded.TimeDivision}, Duration: {loaded.GetDuration<MetricTimeSpan>().Hours:00}:{loaded.GetDuration<MetricTimeSpan>().Minutes:00}:{loaded.GetDuration<MetricTimeSpan>().Seconds:00}:{loaded.GetDuration<MetricTimeSpan>().Milliseconds:000}");
						//foreach (var chunk in loaded.Chunks) PluginLog.LogDebug($"{chunk}");
						var substring = f.Name.Substring(f.Name.LastIndexOf('\\') + 1);
						PlaylistManager.Filelist.Add((loaded, substring.Substring(0, substring.LastIndexOf('.'))));
					}

					PluginLog.LogDebug($"-> {fileName} OK!");
				}
				catch (Exception ex)
				{
					PluginLog.LogError(ex, "Failed to import file at {0}", fileName);
				}
			}



			pluginInterface.Framework.OnUpdateEvent += Tick;


		}

		private void Tick(Dalamud.Game.Internal.Framework framework)
		{
			if (config.AutoOpenPlayerWhenPerforming)
			{
				if (!ui.IsVisible && InPerformanceMode)
				{
					ui.IsVisible = true;
				}
			}

			if (ui.IsVisible)
			{
				configSaverTick++;
				if (configSaverTick == 60 * 60)
				{
					configSaverTick = 0;
					try
					{
						config.Save();
						PluginLog.Debug("config saved.");
					}
					catch (Exception e)
					{
						PluginLog.Debug(e, "error when auto save settings.");
					}
				}
			}

			if (!config.MonitorOnEnsemble) return;

			if (InPerformanceMode)
			{
				if (EnsembleModeRunning)
				{
					wasEnsembleModeRunning = true;
					if (currentPlayback != null)
					{
						if (MetronomeBeatsElapsed < 0)
						{
							try
							{
								if (currentPlayback.GetCurrentTime<MidiTimeSpan>().TimeSpan != 0)
								{
									currentPlayback.MoveToTime(new MidiTimeSpan(0));
									currentPlayback.Stop();
								}
							}
							catch (Exception e)
							{
								//
							}
						}
						else if (MetronomeBeatsElapsed == 0)
						{
							if (currentPlayback.GetCurrentTime<MidiTimeSpan>().TimeSpan == 0)
							{
								currentPlayback.Start();
							}
						}
					}
					else
					{
						if (PlaylistManager.CurrentPlaying != -1)
						{
							currentPlayback = PlaylistManager.Filelist[PlaylistManager.CurrentPlaying].Item1.GetFilePlayback();
						}
					}
				}
				else
				{
					//if (config.AutoConfirmEnsembleReadyCheck)
					{
						var confirmWindow = pluginInterface.Framework.Gui.GetAddonByName("PerformanceReadyCheckReceive", 1);
						if (confirmWindow != null)
						{
							playlib.ConfirmReadyCheck(confirmWindow.Address);
						}
					}

					if (wasEnsembleModeRunning && IsPlaying)
					{
						currentPlayback?.Stop();
					}
					wasEnsembleModeRunning = false;
				}
			}
		}

		[Command("/midibard")]
		[HelpMessage("open midibard window")]
		public void Command1(string command, string args)
		{
			OnCommand(command, args);
		}

		[Command("/mbard")]
		[HelpMessage("open midibard window")]
		public void Command2(string command, string args)
		{
			OnCommand(command, args);
		}

		void OnCommand(string command, string args)
		{
			PluginLog.Information($"{command},{args}");

			var argStrings = args.Split(' ').Select(i => i.ToLower().Trim()).Where(i => !string.IsNullOrWhiteSpace(i)).ToList();
			if (argStrings.Any())
			{
				if (argStrings[0].Contains("load"))
				{

				}
			}
			else
			{
				ui.IsVisible ^= true;
			}
		}



		//[Command("/play")]
		//[HelpMessage("Example help message.")]
		//public void PressNote(string command, string args)
		//{
		//	var num = int.Parse(args.Trim());
		//	var addon = pluginInterface.Framework.Gui.GetAddonByName("PerformanceModeWide", 1);
		//	if (addon is { })
		//	{
		//		playlib.PressKey(addon.Address, num);
		//	}
		//}

		//[Command("/release")]
		//[HelpMessage("Example help message.")]
		//public void ReleaseNote(string command, string args)
		//{
		//	var num = int.Parse(args.Trim());
		//	var addon = pluginInterface.Framework.Gui.GetAddonByName("PerformanceModeWide", 1);
		//	if (addon is { })
		//	{
		//		playlib.ReleaseKey(addon.Address, num);
		//	}
		//}

		#region IDisposable Support
		protected virtual void Dispose(bool disposing)
		{
			if (!disposing) return;

			pluginInterface.Framework.OnUpdateEvent -= Tick;
			try
			{
				currentPlayback?.Stop();
				currentPlayback?.Dispose();
				currentPlayback = null;
			}
			catch (Exception e)
			{
				PluginLog.Error($"{e}");
			}

			AgentManager.Agents.Clear();

			commandManager.Dispose();

			pluginInterface.SavePluginConfig(config);

			pluginInterface.UiBuilder.OnBuildUi -= ui.Draw;

			pluginInterface.Dispose();
		}

		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		#endregion
	}
}