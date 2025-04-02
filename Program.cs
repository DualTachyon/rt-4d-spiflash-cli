using System;
using System.IO.Ports;
using System.Runtime.CompilerServices;

namespace RT_4D_SPIFlash_CLI {
	internal class Program {
		struct Region_t {
			public byte Region;
			public UInt32 Start;
			public UInt32 Size;
			public String Name;
		};

		static Region_t[] Regions = new Region_t[] {
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
		};

		static void Usage()
		{
			string[] Parts = Environment.CommandLine.Split(new char[] { System.IO.Path.DirectorySeparatorChar });
			string Exe = Parts[Parts.Length - 1];
			Exe = Exe.Trim();
			Console.WriteLine("Usage:");
			Console.WriteLine("\t" + Exe + " -l                        List available COM ports");
			Console.WriteLine("\t" + Exe + " -p COMx -r spi.bin        Backup SPI flash");
			Console.WriteLine("\t" + Exe + " -p COMx -w spi.bin        Restore SPI flash");
		}

		static void Main(string[] args)
		{
			byte[] Spi = null;
			System.IO.FileStream F = null;

			Console.WriteLine("RT-4D-SPIFlash-CLI (c) Copyright 2025 Dual Tachyon\n");
			switch (args.Length) {
			case 1:
				if (args[0] != "-l") {
					Usage();
					break;
				}
				var Ports = SerialPort.GetPortNames();
				Console.Write("Ports available:");
				foreach (var Port in Ports) {
					Console.Write(" " + Port);
				}
				Console.WriteLine();
				break;
			case 4:
				if (args[0] != "-p") {
					Usage();
					break;
				}
				if (args[2] != "-r" && args[2] != "-w") {
					Usage();
					break;
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

							Console.WriteLine("Flashing " + Regions[i].Name + "...");
							if (!RT.Command_WriteSpi(Spi, Region, Address, Size)) {
								Console.WriteLine("\rFailed to flash at 0x" + i.ToString("X4") + "!");
								Console.Out.Flush();
								break;
							}
						}
						RT.Command_Close();
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
						Console.WriteLine("\rUnexpected failure writing to flash! Error: ", Ex.Message);
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
