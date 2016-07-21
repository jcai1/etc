﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace etc
{
	public enum Direction
	{
		BUY,
		SELL
	};

	public class HelloEventArgs : EventArgs
	{
		public int cash;
		public Dictionary<string, int> positions; // key = symbol, value = position
	}

	public class OpenEventArgs : EventArgs
	{
		public List<string> symbols;
	}

	public class CloseEventArgs : EventArgs
	{
		public List<string> symbols;
	}

	public class ErrorEventArgs : EventArgs
	{
		public string message;
	}

	public class BookEventArgs : EventArgs
	{
		public string symbol;
		public SortedDictionary<int, int> buys; // key = price, value = quantity
		public SortedDictionary<int, int> sells;
	}

	public class TradeEventArgs : EventArgs
	{
		public string symbol;
		public int price;
		public int size;
	}

	public class AckEventArgs : EventArgs
	{
		public int id;
	}

	public class RejectEventArgs : EventArgs
	{
		public int id;
		public string message;
	}

	public class FillEventArgs : EventArgs
	{
		public int id;
		public string symbol;
		public Direction dir;
		public int price;
		public int size;
	}

	public class OutEventArgs : EventArgs
	{
		public int id;
	}

	class Market
	{
		public delegate void HelloEventHandler(object sender, HelloEventArgs e);
		public delegate void OpenEventHandler(object sender, OpenEventArgs e);
		public delegate void CloseEventHandler(object sender, CloseEventArgs e);
		public delegate void ErrorEventHandler(object sender, ErrorEventArgs e);
		public delegate void BookEventHandler(object sender, BookEventArgs e);
		public delegate void TradeEventHandler(object sender, TradeEventArgs e);
		public delegate void AckEventHandler(object sender, AckEventArgs e);
		public delegate void RejectEventHandler(object sender, RejectEventArgs e);
		public delegate void FillEventHandler(object sender, FillEventArgs e);
		public delegate void OutEventHandler(object sender, OutEventArgs e);

		public event HelloEventHandler GotHello;
		public event OpenEventHandler Open;
		public event CloseEventHandler Close;
		public event ErrorEventHandler Error;
		public event BookEventHandler Book;
		public event TradeEventHandler Trade;
		public event AckEventHandler Ack;
		public event RejectEventHandler Reject;
		public event FillEventHandler Fill;
		public event OutEventHandler Out;

		private NetworkStream stream;
		private StreamReader reader;
		private StreamWriter writer;

		const int INVALID_ID = -1;
		private int currentID = 0;

		public Market(NetworkStream stream_)
		{
			stream = stream_;
			reader = new StreamReader(stream, Encoding.ASCII);
			writer = new StreamWriter(stream, Encoding.ASCII);
		}

		private void LogSend(string msg)
		{
			Console.WriteLine("SEND: " + msg);
		}

		private void LogReceive(string msg)
		{
			Console.WriteLine("RECV: " + msg);
		}

		private void LogError(string msg)
		{
			Console.Error.WriteLine("ERROR: " + msg);
		}

		public Direction ParseDirection(string s)
		{
			return (Direction)Enum.Parse(typeof(Direction), s);
		}

		public void ReceiveLoop()
		{
			string msg;
			while ((msg = reader.ReadLine()) != null)
			{
				LogReceive(msg);
				string[] toks = msg.Split(' ');
				try
				{
					switch (toks[0].ToUpper())
					{
						case "HELLO":
							{
								var args = new HelloEventArgs();
								args.cash = int.Parse(toks[1]);
								args.positions = new Dictionary<string, int>();
								for (int i = 2; i < toks.Length; ++i)
								{
									string[] symAndPosn = toks[i].Split(':');
									args.positions.Add(symAndPosn[0], int.Parse(symAndPosn[1]));
								}
								var handler = GotHello;
								if (handler != null) handler(this, args);
								break;
							}
						case "OPEN":
							{
								var args = new OpenEventArgs();
								args.symbols = new List<string>();
								for (int i = 1; i < toks.Length; ++i)
								{
									args.symbols.Add(toks[i]);
								}
								var handler = Open;
								if (handler != null) handler(this, args);
								break;
							}
						case "CLOSE":
							{
								var args = new CloseEventArgs();
								args.symbols = new List<string>();
								for (int i = 1; i < toks.Length; ++i)
								{
									args.symbols.Add(toks[i]);
								}
								var handler = Close;
								if (handler != null) handler(this, args);
								break;
							}
						case "ERROR":
							{
								var args = new ErrorEventArgs();
								args.message = msg.Substring(6);
								var handler = Error;
								if (handler != null) handler(this, args);
								break;
							}
						case "BOOK":
							{
								var args = new BookEventArgs();
								args.symbol = toks[1];
								args.buys = new SortedDictionary<int, int>();
								args.sells = new SortedDictionary<int, int>();
								if (toks[2] != "BUY") throw new Exception("toks[2] is not BUY");
								int i;
								for (i = 3; i < toks.Length; ++i)
								{
									if (toks[i].ToUpper() == "SELL") { break; }
									string[] priceAndSize = toks[i].Split(':');
									args.buys.Add(int.Parse(priceAndSize[0]), int.Parse(priceAndSize[1]));
								}
								++i;
								for (; i < toks.Length; ++i)
								{
									string[] priceAndSize = toks[i].Split(':');
									args.sells.Add(int.Parse(priceAndSize[0]), int.Parse(priceAndSize[1]));
								}
								var handler = Book;
								if (handler != null) handler(this, args);
								break;
							}
						case "TRADE":
							{
								var args = new TradeEventArgs();
								args.symbol = toks[1];
								args.price = int.Parse(toks[2]);
								args.size = int.Parse(toks[3]);
								var handler = Trade;
								if (handler != null) handler(this, args);
								break;
							}
						case "ACK":
							{
								var args = new AckEventArgs();
								args.id = int.Parse(toks[1]);
								var handler = Ack;
								if (handler != null) handler(this, args);
								break;
							}
						case "REJECT":
							{
								var args = new RejectEventArgs();
								args.id = int.Parse(toks[1]);
								string[] remainingToks = new string[toks.Length - 2];
								for (int i = 2; i < toks.Length; ++i) remainingToks[i - 2] = toks[i];
								args.message = string.Join(" ", remainingToks);
								var handler = Reject;
								if (handler != null) handler(this, args);
								break;
							}
						case "FILL":
							{
								var args = new FillEventArgs();
								args.id = int.Parse(toks[1]);
								args.symbol = toks[2];
								args.dir = ParseDirection(toks[3]);
								args.price = int.Parse(toks[4]);
								args.size = int.Parse(toks[5]);
								var handler = Fill;
								if (handler != null) handler(this, args);
								break;
							}
						case "OUT":
							{
								var args = new OutEventArgs();
								args.id = int.Parse(toks[1]);
								var handler = Out;
								if (handler != null) handler(this, args);
								break;
							}
					}
				}
				catch (Exception ex)
				{
					LogError("Exn in Receive processing: " + ex.Message);
				}
			}
		}

		public void Hello()
		{
			string msg = "HELLO AMPERE";

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}

		public int Add(string symbol, Direction dir, int price, int size)
		{
			if (size < 0)
			{
				LogError("ADD with negative size attempted");
				return INVALID_ID;
			}

			if (price < 0)
			{
				LogError("ADD with negative price attempted");
				return INVALID_ID;
			}

			int id = Interlocked.Increment(ref currentID);
			string msg = string.Format("ADD {0} {1} {2} {3} {4}", id, symbol.ToUpper(), dir, price, size);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
			return id;
		}

		public int Convert(string symbol, Direction dir, int size)
		{
			if (size < 0)
			{
				LogError("CONVERT with negative size attempted");
				return INVALID_ID;
			}

			int id = Interlocked.Increment(ref currentID);
			string msg = string.Format("CONVERT {0} {1} {2} {3}", id, symbol.ToUpper(), dir, size);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
			return id;
		}

		public void Cancel(int id)
		{
			string msg = string.Format("CANCEL {0}", id);
			Console.WriteLine("SEND: " + msg);

			writer.WriteLine(msg);
			writer.Flush();
			LogSend(msg);
		}
	}
}
