/* Copyright 2025 Dual Tachyon
 * https://github.com/DualTachyon
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 *     Unless required by applicable law or agreed to in writing, software
 *     distributed under the License is distributed on an "AS IS" BASIS,
 *     WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *     See the License for the specific language governing permissions and
 *     limitations under the License.
 */

using System;
using System.IO.Ports;

namespace RT_4D_SPIFlash_CLI {
	internal class Program {
		struct Region_t {
			public byte Region;
			public UInt32 Start;
			public UInt32 Size;
			public String Name;
		};

		static Region_t[] Regions = new Region_t[] {
			new Region_t { Region = 0x40, Start = 0x000000, Size = 0x001000, Name = "Calibration" },

			new Region_t { Region = 0x90, Start = 0x002000, Size = 0x001000, Name = "Main settings" },
			new Region_t { Region = 0x91, Start = 0x004000, Size = 0x00C000, Name = "Channels" },
			new Region_t { Region = 0x92, Start = 0x01C000, Size = 0x020000, Name = "Zones" },
			new Region_t { Region = 0x93, Start = 0x05C000, Size = 0x010000, Name = "Contacts" },
			new Region_t { Region = 0x94, Start = 0x07C000, Size = 0x003000, Name = "Groups" },
			new Region_t { Region = 0x95, Start = 0x082000, Size = 0x003000, Name = "DMR Keys" },
			new Region_t { Region = 0x96, Start = 0x088000, Size = 0x00C000, Name = "Call log" },
			new Region_t { Region = 0x97, Start = 0x094000, Size = 0x001000, Name = "Default SMS" },
			new Region_t { Region = 0x98, Start = 0x0C6000, Size = 0x008000, Name = "Schedules" },
			new Region_t { Region = 0x99, Start = 0x0D6000, Size = 0x001000, Name = "FM settings" },

			new Region_t { Region = 0x9A, Start = 0x100000, Size = 0x001000, Name = "Boot Logo" },
			new Region_t { Region = 0x9B, Start = 0x126000, Size = 0x001000, Name = "???" },
			new Region_t { Region = 0x9C, Start = 0x14C000, Size = 0x018000, Name = "UTF16" },
			new Region_t { Region = 0x9D, Start = 0x164000, Size = 0x024000, Name = "Phonemes" },
			new Region_t { Region = 0x9E, Start = 0x188000, Size = 0x010000, Name = "???" },
			new Region_t { Region = 0x9F, Start = 0x198000, Size = 0x004000, Name = "???" },
			new Region_t { Region = 0xA0, Start = 0x19C000, Size = 0x002000, Name = "Fonts #1" },
			new Region_t { Region = 0xA1, Start = 0x19E000, Size = 0x0B4000, Name = "Fonts #2" },
			new Region_t { Region = 0xA2, Start = 0x343000, Size = 0x00F000, Name = "???" },
			new Region_t { Region = 0xA3, Start = 0x352000, Size = 0x0AE000, Name = "Voices" },
			//new Region_t { Region = 0xA4, Start = 0x400000, Size = 0xC00000, Name = "Address book" },
		};

		static void Usage()
		{
			string[] Parts = Environment.CommandLine.Split(new char[] { System.IO.Path.DirectorySeparatorChar });
			string Exe = Parts[Parts.Length - 1];
			Exe = Exe.Trim();
			Console.WriteLine("Usage:");
			Console.WriteLine("\t" + Exe + " -l                        List available COM ports");
			Console.WriteLine("\t" + Exe + " -i                        List of indices that can be restored");
			Console.WriteLine("\t" + Exe + " -p COMx -r spi.bin        Backup SPI flash");
			Console.WriteLine("\t" + Exe + " -p COMx -w spi.bin        Restore SPI flash");
			Console.WriteLine("\t" + Exe + " -p COMx -w spi.bin -i X   Restore only region X to SPI flash");
		}

		static void Main(string[] args)
		{
			byte[] Spi = null;
			System.IO.FileStream F = null;
			byte RegionIndex = 0xFF;

			Console.WriteLine("RT-4D-SPIFlash-CLI (c) Copyright 2025 Dual Tachyon\n");
			switch (args.Length) {
			case 1:
				if (args[0] == "-l") {
					var Ports = SerialPort.GetPortNames();
					Console.Write("Ports available:");
					foreach (var Port in Ports) {
						Console.Write(" " + Port);
					}
					Console.WriteLine();
				} else if (args[0] == "-i") {
					RegionIndex = 0;
					foreach (var Region in Regions) {
						Console.WriteLine($"    {RegionIndex:D2} - {Region.Name}");
						RegionIndex++;
					}
				} else {
					Usage();
				}
				break;
			case 4:
			case 6:
				if (args[0] != "-p") {
					Usage();
					break;
				}
				if (args[2] != "-r" && args[2] != "-w") {
					Usage();
					break;
				}

				if (args.Length == 6) {
					byte R;

					if (args[2] != "-w" || args[4] != "-i" || !byte.TryParse(args[5], out R) || R >= Regions.Length) {
						Usage();
						break;
					}
					RegionIndex = R;
				}
				if (args[2] == "-w") {
					try {
						Spi = System.IO.File.ReadAllBytes(args[3]);
					} catch {
						Console.WriteLine("Failed to read file!");
						break;
					}
				} else {
					try {
						F = new System.IO.FileStream(args[3], System.IO.FileMode.Create);
					} catch {
						Console.WriteLine("Failed to create file '" + args[3] + "' !");
						break;
					}
				}

				RT_4D_UART RT = new RT_4D_UART();
				try {
					RT.Open(args[1]);
				} catch {
					Console.WriteLine("Failed to open COM port!");
					break;
				}

				try {
					if (RT.IsBootLoaderMode()) {
						Console.WriteLine("RT-4D is not in normal mode!");
						break;
					}
				} catch {
					Console.WriteLine("Timeout error! Is the radio in normal mode?");
					break;
				}

				RT.Command_Notify();

				if (args[2] == "-w") {
					try {
						ushort i;

						for (i = 0; i < Regions.Length; i++) {
							byte Region = Regions[i].Region;
							UInt32 Address = Regions[i].Start;
							UInt32 Size = Regions[i].Size;

							if (RegionIndex != 0xFF && i != RegionIndex) {
								continue;
							}
							Console.WriteLine("Flashing " + Regions[i].Name + "...");
							if (!RT.Command_WriteSpi(Spi, Region, Address, Size)) {
								Console.WriteLine("\rFailed to flash!");
								Console.Out.Flush();
								break;
							}
						}
						if (i == Regions.Length) {
							RT.Command_Close();
						}
					} catch (Exception Ex) {
						Console.WriteLine("\rUnexpected failure writing to SPI flash! Error: ", Ex.Message);
					}
				} else {
					try {
						ushort i;

						for (i = 0; i < 4096; i++) {
							float Percentage = (i / 4096f) * 100f;
							Console.Write($"\rReading SPI flash at {Percentage:F1}%");
							Console.Out.Flush();
							byte[] Data = RT.Command_ReadSpi(i);
							if (Data == null) {
								Console.WriteLine("\rFailed to read SPI flash at 0x" + i.ToString("X4") + "!");
								Console.Out.Flush();
								break;
							}
							F.Write(Data, 0, Data.Length);
						}
						if (i == 4096) {
							Console.WriteLine("\rDone                       ");
							RT.Command_Close();
						}
					} catch (Exception Ex) {
						Console.WriteLine("\rUnexpected failure reading from SPI flash! Error: ", Ex.Message);
					}
				}
				Console.WriteLine();
				RT.Close();
				break;

			default:
				Usage();
				break;
			}
		}
	}
}
