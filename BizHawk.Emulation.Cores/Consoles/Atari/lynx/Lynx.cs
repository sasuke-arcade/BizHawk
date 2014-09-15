﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;

using BizHawk.Common;
using BizHawk.Emulation.Common;

namespace BizHawk.Emulation.Cores.Atari.Lynx
{
	[CoreAttributes("Handy", "K. Wilkins", true, false, "mednafen 0-9-34-1", "http://mednafen.sourceforge.net/")]
	public class Lynx : IEmulator, IVideoProvider, ISyncSoundProvider
	{
		IntPtr Core;

		[CoreConstructor("LYNX")]
		public Lynx(byte[] file, GameInfo game, CoreComm comm)
		{
			CoreComm = comm;

			byte[] bios = CoreComm.CoreFileProvider.GetFirmware("LYNX", "Boot", true, "Boot rom is required");
			if (bios.Length != 512)
				throw new MissingFirmwareException("Lynx Bootrom must be 512 bytes!");

			int pagesize0 = 0;
			int pagesize1 = 0;
			byte[] realfile = null;

			{
				var ms = new MemoryStream(file, false);
				var br = new BinaryReader(ms);
				string header = Encoding.ASCII.GetString(br.ReadBytes(4));
				int p0 = br.ReadUInt16();
				int p1 = br.ReadUInt16();
				int ver = br.ReadUInt16();
				string cname = Encoding.ASCII.GetString(br.ReadBytes(32)).Trim();
				string mname = Encoding.ASCII.GetString(br.ReadBytes(16)).Trim();
				int rot = br.ReadByte();

				ms.Position = 6;
				string bs93 = Encoding.ASCII.GetString(br.ReadBytes(6));
				if (bs93 == "BS93")
					throw new InvalidOperationException("Unsupported BS93 Lynx ram image");

				if (header == "LYNX" && (ver & 255) == 1)
				{
					Console.WriteLine("Processing Handy-Lynx header");
					pagesize0 = p0;
					pagesize1 = p1;
					Console.WriteLine("TODO: Rotate {0}", rot);
					Console.WriteLine("Cart: {0} Manufacturer: {1}", cname, mname);
					realfile = new byte[file.Length - 64];
					Buffer.BlockCopy(file, 64, realfile, 0, realfile.Length);
					Console.WriteLine("Header Listed banking: {0} {1}", p0, p1);
				}
				else
				{
					Console.WriteLine("No Handy-Lynx header found!  Assuming raw rom image.");
					realfile = file;
				}

			}

			if (game.OptionPresent("pagesize0"))
			{
				pagesize0 = int.Parse(game.OptionValue("pagesize0"));
				pagesize1 = int.Parse(game.OptionValue("pagesize1"));
				Console.WriteLine("Loading banking options {0} {1} from gamedb", pagesize0, pagesize1);
			}

			if (pagesize0 == 0 && pagesize1 == 0)
			{
				switch (realfile.Length)
				{
					// these are untested
					case 0x10000: pagesize0 = 0x100; break;
					case 0x20000: pagesize0 = 0x200; break;
					case 0x40000: pagesize0 = 0x400; break;
					case 0x80000: pagesize0 = 0x800; break;

					case 0x30000: pagesize0 = 0x200; pagesize1 = 0x100; break;
					case 0x60000: pagesize0 = 0x400; pagesize1 = 0x200; break;
					case 0xc0000: pagesize0 = 0x800; pagesize1 = 0x400; break;
					case 0x100000: pagesize0 = 0x800; pagesize1 = 0x800; break;

				}
				Console.WriteLine("Auto-guessed banking options {0} {1}", pagesize0, pagesize1);
			}

			Core = LibLynx.Create(realfile, realfile.Length, bios, bios.Length, pagesize0, pagesize1, false);
			try
			{
				// ...
			}
			catch
			{
				Dispose();
				throw;
			}
		}

		public void FrameAdvance(bool render, bool rendersound = true)
		{
			Frame++;

			if (Controller["Power"])
				LibLynx.Reset(Core);

			int samples = soundbuff.Length;
			LibLynx.Advance(Core, 0, videobuff, soundbuff, ref samples);
			numsamp = samples;
			Console.WriteLine(numsamp);
		}

		public int Frame { get; private set; }
		public int LagCount { get; set; }
		public bool IsLagFrame { get; private set; }

		public string SystemId { get { return "LYNX"; } }

		public bool DeterministicEmulation { get { return true; } }

		public void ResetCounters()
		{
			Frame = 0;
			LagCount = 0;
			IsLagFrame = false;
		}

		public string BoardName { get { return null; } }

		public CoreComm CoreComm { get; private set; }

		public void Dispose()
		{
			if (Core != IntPtr.Zero)
			{
				LibLynx.Destroy(Core);
				Core = IntPtr.Zero;
			}
		}

		#region debugging

		public Dictionary<string, int> GetCpuFlagsAndRegisters()
		{
			return new Dictionary<string, int>();
		}

		public void SetCpuRegister(string register, int value)
		{
		}

		#endregion

		#region Controller

		public ControllerDefinition ControllerDefinition { get { return NullEmulator.NullController; } }
		public IController Controller { get; set; }

		#endregion

		#region savestates

		public void SaveStateText(TextWriter writer)
		{
		}

		public void LoadStateText(TextReader reader)
		{
		}

		public void SaveStateBinary(BinaryWriter writer)
		{
		}

		public void LoadStateBinary(BinaryReader reader)
		{
		}

		public byte[] SaveStateBinary()
		{
			return new byte[0];
		}

		public bool BinarySaveStatesPreferred
		{
			get { return true; }
		}

		#endregion

		#region saveram

		public byte[] CloneSaveRam()
		{
			return new byte[0];
		}

		public void StoreSaveRam(byte[] data)
		{
		}

		public void ClearSaveRam()
		{
		}

		public bool SaveRamModified
		{
			get
			{
				return false;
			}
			set
			{
				throw new InvalidOperationException();
			}
		}

		#endregion

		#region VideoProvider

		const int WIDTH = 160;
		const int HEIGHT = 102;

		int[] videobuff = new int[WIDTH * HEIGHT];

		public IVideoProvider VideoProvider { get { return this; } }
		public int[] GetVideoBuffer() { return videobuff; }
		public int VirtualWidth { get { return WIDTH; } }
		public int VirtualHeight { get { return HEIGHT; } }
		public int BufferWidth { get { return WIDTH; } }
		public int BufferHeight { get { return HEIGHT; } }
		public int BackgroundColor { get { return unchecked((int)0xff000000); } }

		#endregion

		#region SoundProvider

		short[] soundbuff = new short[1000000]; // todo: make this smaller once frame loop is resolved
		int numsamp;

		public ISoundProvider SoundProvider { get { return null; } }
		public ISyncSoundProvider SyncSoundProvider { get { return this; } }
		public bool StartAsyncSound() { return false; }
		public void EndAsyncSound() { }

		public void GetSamples(out short[] samples, out int nsamp)
		{
			samples = soundbuff;
			nsamp = numsamp;
		}

		public void DiscardSamples()
		{
		}

		#endregion

		#region Settings

		public object GetSettings()
		{
			return null;
		}

		public object GetSyncSettings()
		{
			return null;
		}

		public bool PutSettings(object o)
		{
			return false;
		}

		public bool PutSyncSettings(object o)
		{
			return false;
		}

		#endregion

	}
}
