﻿using SimpleTCP;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.LinkLabel;

namespace ListOfNames
{
	public partial class Server : Form
	{
		private SimpleTcpServer _server;
		private List<Record> _records = new List<Record>();
		private string _csvFile = @"records.csv";
		private int _id;

		public Server()
		{
			InitializeComponent();
		}

		private void Server_Load(object sender, EventArgs e)
		{
			_server = new SimpleTcpServer();
			dataGridView1.AutoGenerateColumns = true;
			btn_stop.Enabled = false;
		}

		private void btn_start_Click(object sender, EventArgs e)
		{
			if (!File.Exists(_csvFile))
			{
				var csvFile = File.Create(_csvFile);
				csvFile.Close();
			}

			//Console clean up
			txt_box_server_console.Clear();

			btn_start.Enabled = false;
			btn_stop.Enabled = true;
			txt_box_ip.Enabled = false;
			txt_box_port.Enabled = false;

			try
			{
				IPAddress ip = IPAddress.Parse(txt_box_ip.Text);
				_server.Start(ip, Convert.ToInt32(txt_box_port.Text));
				UpdateConsoleText($"Server starting...");
			}
			catch (Exception ex)
			{
				UpdateConsoleText($"WARNING: {ex.Message}");
				return;
			}

			LoadDataFromCsvIntoRecords(_csvFile);
			dataGridView1.DataSource = _records;

			//Get last id
			_id = _records.Count > 0 ? _records.Last().Id : 0;

			_server.DataReceived += HandleRecievedData;
		}

		private void btn_stop_Click(object sender, EventArgs e)
		{
			btn_start.Enabled = true;
			btn_stop.Enabled = false;

			txt_box_ip.Enabled = true;
			txt_box_port.Enabled = true;

			_server.Stop();

			//Clean up
			dataGridView1.DataSource = null;
			_records.Clear();
			UpdateConsoleText("Server closing...");
		}

		private void HandleRecievedData(object sender, SimpleTCP.Message e)
		{
			string[] dataParts = e.MessageString.Split(',');

			if (dataParts.Length == 3 && dataParts.First() == "CREATE")
			{
				AddRecordToCsv(dataParts[1], dataParts[2].Replace("\u0013", ""));
				e.ReplyLine("CREATED");
			}
			else if (dataParts.First().Replace("\u0013", "") == "DELETE" && dataGridView1.SelectedRows.Count == 1)
			{
				DeleteRecordFromCsv((int)dataGridView1.SelectedCells[0].Value);
                e.ReplyLine("DELETED");
            }
			else if (dataParts.Length == 3 && dataParts.First() == "EDIT" && dataGridView1.SelectedRows.Count == 1)
			{
				EditRecordToCsv((int)dataGridView1.SelectedCells[0].Value, dataParts[1], dataParts[2].Replace("\u0013", ""));
				e.ReplyLine("EDITED");
			}

			RefreshData();
		}

		private void RefreshData()
		{
			//Refresh data
			dataGridView1.Invoke(new Action(() => {
				dataGridView1.DataSource = null;
				_records.Clear();
				LoadDataFromCsvIntoRecords(_csvFile);
				dataGridView1.DataSource = _records;
			}));
		}

		private void AddRecordToCsv(string firstName, string lastName)
		{
			using (StreamWriter writer = new StreamWriter(_csvFile, true))
			{
				_id++;
				writer.WriteLine($"{_id},{firstName},{lastName}");
				UpdateConsoleText("Record created!");
			}
		}

		private void DeleteRecordFromCsv(int idToDelete)
		{
			bool found = false;
			List<string> lines = new List<string>();

			foreach (string line in File.ReadAllLines(_csvFile))
			{
				string[] parts = line.Split(',');
				if (int.TryParse(parts[0], out int id) && id == idToDelete)
				{
					found = true;
					continue; //Skip the line with the specified ID
				}

				lines.Add(line);
			}

			if (found)
			{
				File.WriteAllLines(_csvFile, lines);
				UpdateConsoleText("Record deleted!");
			}
			else
				UpdateConsoleText("WARNING: Record not found!");
		}

		private void EditRecordToCsv(int idToEdit, string firstName, string lastName)
		{
			List<string> modifiedLines = new List<string>();

			foreach (string line in File.ReadAllLines(_csvFile))
			{
				string[] parts = line.Split(',');
				if (int.TryParse(parts[0], out int id) && id == idToEdit)
					modifiedLines.Add($"{idToEdit},{firstName},{lastName}");

				else
					modifiedLines.Add(line);
			}

			File.WriteAllLines(_csvFile, modifiedLines);
			UpdateConsoleText("Record edited!");
		}

		private void LoadDataFromCsvIntoRecords(string filePath)
		{
			using (StreamReader reader = new StreamReader(filePath))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					string[] fields = line.Split(',');

					if (fields.Length == 3)
					{
						int id = int.Parse(fields[0]);
						string firstname = fields[1];
						string lastname = fields[2];

						_records.Add(new Record(id, firstname, lastname));
					}
				}
			}
			UpdateConsoleText($"Data loaded from file: {Path.GetFullPath(_csvFile)}");
		}

		private void UpdateConsoleText(string message)
		{
			if (txt_box_server_console.InvokeRequired)
				txt_box_server_console.Invoke(new Action<string>(UpdateConsoleText), $"{message}{Environment.NewLine}");
			else
				txt_box_server_console.Text += $"{DateTime.Now}: {message}{Environment.NewLine}";
		}
	}
}
