﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

using BizHawk.Client.Common;

namespace BizHawk.Client.EmuHawk
{
	class ToolHelpers
	{
		public static FileInfo GetWatchFileFromUser(string currentFile)
		{
			var ofd = new OpenFileDialog();
			if (!String.IsNullOrWhiteSpace(currentFile))
			{
				ofd.FileName = Path.GetFileNameWithoutExtension(currentFile);
			}
			ofd.InitialDirectory = PathManager.MakeAbsolutePath(Global.Config.PathEntries.WatchPath, null);
			ofd.Filter = "Watch Files (*.wch)|*.wch|All Files|*.*";
			ofd.RestoreDirectory = true;

			GlobalWin.Sound.StopSound();
			var result = ofd.ShowDialog();
			GlobalWin.Sound.StartSound();
			if (result != DialogResult.OK)
				return null;
			var file = new FileInfo(ofd.FileName);
			return file;
		}

		public static FileInfo GetWatchSaveFileFromUser(string currentFile)
		{
			var sfd = new SaveFileDialog();
			if (!String.IsNullOrWhiteSpace(currentFile))
			{
				sfd.FileName = Path.GetFileNameWithoutExtension(currentFile);
				sfd.InitialDirectory = Path.GetDirectoryName(currentFile);
			}
			else if (!(Global.Emulator is NullEmulator))
			{
				sfd.FileName = PathManager.FilesystemSafeName(Global.Game);
				sfd.InitialDirectory = PathManager.MakeAbsolutePath(Global.Config.PathEntries.WatchPath, null);
			}
			else
			{
				sfd.FileName = "NULL";
				sfd.InitialDirectory = PathManager.MakeAbsolutePath(Global.Config.PathEntries.WatchPath, null);
			}
			sfd.Filter = "Watch Files (*.wch)|*.wch|All Files|*.*";
			sfd.RestoreDirectory = true;
			GlobalWin.Sound.StopSound();
			var result = sfd.ShowDialog();
			GlobalWin.Sound.StartSound();
			if (result != DialogResult.OK)
				return null;
			var file = new FileInfo(sfd.FileName);
			return file;
		}

		public static FileInfo GetCheatFileFromUser(string currentFile)
		{
			var ofd = new OpenFileDialog();
			if (!String.IsNullOrWhiteSpace(currentFile))
			{
				ofd.FileName = Path.GetFileNameWithoutExtension(currentFile);
			}
			ofd.InitialDirectory = PathManager.GetCheatsPath(Global.Game);
			ofd.Filter = "Cheat Files (*.cht)|*.cht|All Files|*.*";
			ofd.RestoreDirectory = true;

			GlobalWin.Sound.StopSound();
			var result = ofd.ShowDialog();
			GlobalWin.Sound.StartSound();
			if (result != DialogResult.OK)
				return null;
			var file = new FileInfo(ofd.FileName);
			return file;
		}

		public static FileInfo GetCheatSaveFileFromUser(string currentFile)
		{
			var sfd = new SaveFileDialog();
			if (!String.IsNullOrWhiteSpace(currentFile))
			{
				sfd.FileName = Path.GetFileNameWithoutExtension(currentFile);
			}
			else if (!(Global.Emulator is NullEmulator))
			{
				sfd.FileName = PathManager.FilesystemSafeName(Global.Game);
			}
			sfd.InitialDirectory = PathManager.GetCheatsPath(Global.Game);
			sfd.Filter = "Cheat Files (*.cht)|*.cht|All Files|*.*";
			sfd.RestoreDirectory = true;
			GlobalWin.Sound.StopSound();
			var result = sfd.ShowDialog();
			GlobalWin.Sound.StartSound();
			if (result != DialogResult.OK)
			{
				return null;
			}

			var file = new FileInfo(sfd.FileName);
			Global.Config.LastRomPath = file.DirectoryName;
			return file;
		}

		public static ToolStripMenuItem GenerateAutoLoadItem(RecentFiles recent)
		{
			var auto = new ToolStripMenuItem { Text = "&Auto-Load", Checked = recent.AutoLoad };
			auto.Click += (o, ev) => recent.ToggleAutoLoad();
			return auto;
		}

		public static ToolStripItem[] GenerateRecentMenu(RecentFiles recent, Action<string> loadFileCallback)
		{
			var items = new List<ToolStripItem>();

			if (recent.Empty)
			{
				var none = new ToolStripMenuItem { Enabled = false, Text = "None" };
				items.Add(none);
			}
			else
			{
				foreach (string filename in recent)
				{
					string temp = filename;
					var item = new ToolStripMenuItem { Text = temp };
					item.Click += (o, ev) => loadFileCallback(temp);
					items.Add(item);
				}
			}

			items.Add(new ToolStripSeparator());

			var clearitem = new ToolStripMenuItem { Text = "&Clear" };
			clearitem.Click += (o, ev) => recent.Clear();
			items.Add(clearitem);

			return items.ToArray();
		}

		public static void HandleLoadError(RecentFiles recent, string path)
		{
			GlobalWin.Sound.StopSound();
			DialogResult result = MessageBox.Show("Could not open " + path + "\nRemove from list?", "File not found", MessageBoxButtons.YesNo, MessageBoxIcon.Error);
			if (result == DialogResult.Yes)
			{
				recent.Remove(path);
			}

			GlobalWin.Sound.StartSound();
		}

		public static ToolStripMenuItem[] GenerateMemoryDomainMenuItems(Action<int> SetCallback, string SelectedDomain = "", int? maxSize = null)
		{
			var items = new List<ToolStripMenuItem>();

			if (Global.Emulator.MemoryDomains.Any())
			{
				int counter = 0;
				foreach (var domain in Global.Emulator.MemoryDomains)
				{
					string temp = domain.ToString();
					var item = new ToolStripMenuItem { Text = temp };

					int index = counter;
					item.Click += (o, ev) => SetCallback(index);

					if (temp == SelectedDomain)
					{
						item.Checked = true;
					}

					if (maxSize.HasValue && domain.Size > maxSize.Value)
					{
						item.Enabled = false;
					}

					items.Add(item);
					counter++;
				}
			}

			return items.ToArray();
		}

		public static void PopulateMemoryDomainDropdown(ref ComboBox dropdown, MemoryDomain startDomain)
		{
			dropdown.Items.Clear();
			if (Global.Emulator.MemoryDomains.Count > 0)
			{
				foreach (var domain in Global.Emulator.MemoryDomains)
				{
					var result = dropdown.Items.Add(domain.ToString());
					if (domain.Name == startDomain.Name)
					{
						dropdown.SelectedIndex = result;
					}
				}
			}
		}

		public static void UpdateCheatRelatedTools()
		{
			GlobalWin.Tools.UpdateValues<RamWatch>();
			GlobalWin.Tools.UpdateValues<RamSearch>();
			GlobalWin.Tools.UpdateValues<HexEditor>();
			GlobalWin.Tools.UpdateValues<Cheats>();
			GlobalWin.MainForm.UpdateCheatStatus();
		}

		public static void UnfreezeAll()
		{
			Global.CheatList.DisableAll();
			UpdateCheatRelatedTools();
		}

		public static void FreezeAddress(List<Watch> watches)
		{
			foreach(var watch in watches)
			{
				if (!watch.IsSeparator)
				{
					Global.CheatList.Add(
						new Cheat(watch, watch.Value.Value, compare: null, enabled: true)
					);
				}
			}

			UpdateCheatRelatedTools();
		}

		public static void UnfreezeAddress(List<Watch> watches)
		{
			foreach (var watch in watches)
			{
				if (!watch.IsSeparator)
				{
					Global.CheatList.Remove(watch);
				}
			}

			UpdateCheatRelatedTools();
		}

		public static void ViewInHexEditor(MemoryDomain domain, IEnumerable<int> addresses)
		{
			GlobalWin.Tools.Load<HexEditor>();
			GlobalWin.Tools.HexEditor.SetDomain(domain);
			GlobalWin.Tools.HexEditor.SetToAddresses(addresses.ToList());
		}

		public static MemoryDomain DomainByName(string name)
		{
			//Attempts to find the memory domain by name, if it fails, it defaults to index 0
			foreach (MemoryDomain domain in Global.Emulator.MemoryDomains)
			{
				if (domain.Name == name)
				{
					return domain;
				}
			}

			return Global.Emulator.MainMemory;
		}

		public static void AddColumn(ListView listView, string columnName, bool enabled, int columnWidth)
		{
			if (enabled)
			{
				if (listView.Columns[columnName] == null)
				{
					ColumnHeader column = new ColumnHeader
					{
						Name = columnName,
						Text = columnName.Replace("Column", ""),
						Width = columnWidth,
					};

					listView.Columns.Add(column);
				}
			}
		}
	}
}
