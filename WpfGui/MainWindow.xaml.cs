using iText.Layout.Element;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		bool m_useSizeOfFirstPic = true;
		int m_pageSizeType = 2;
		int m_totalCnt = 0;
		int m_finishCnt = 0;

		public MainWindow() {
			InitializeComponent();
			ChkBoxUseSizeOfFirstPic.IsChecked = true;
			RadioBtnFixedWidth.IsChecked = true;
			ProgBarSingle.Value = 10.0;
			PicMergeToPdf.Process.SingleUpdate += UpdateSingleBar;
		}

		private void TextNum_PreviewKeyDown(object sender, KeyEventArgs e) {
			bool isNum = e.Key >= Key.D0 && e.Key <= Key.D9;
			bool isNumPad = e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9;
			bool isControl = e.Key == Key.Back || e.Key == Key.Enter || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Decimal;
			if (isNum || isNumPad || isControl) {
				return;
			}
			e.Handled = true;
		}

		private void BtnPageSize_Changed(object sender, RoutedEventArgs e) {
			if (RadioBtnAutoSize.IsChecked == true)
				m_pageSizeType = 1;
			else if (RadioBtnFixedWidth.IsChecked == true)
				m_pageSizeType = 2;
			else if (RadioBtnFixedSize.IsChecked == true)
				m_pageSizeType = 3;
			switch (m_pageSizeType) {
			default:
			case 1:
				ChkBoxUseSizeOfFirstPic.IsEnabled = false;
				TextWidth.IsEnabled = false;
				LabelWidth.IsEnabled = false;
				TextHeight.IsEnabled = false;
				LabelHeight.IsEnabled = false;
				break;
			case 2:
				ChkBoxUseSizeOfFirstPic.IsEnabled = true;
				TextWidth.IsEnabled = true;
				LabelWidth.IsEnabled = true;
				TextHeight.IsEnabled = false;
				LabelHeight.IsEnabled = false;
				break;
			case 3:
				ChkBoxUseSizeOfFirstPic.IsEnabled = true;
				TextWidth.IsEnabled = true;
				LabelWidth.IsEnabled = true;
				TextHeight.IsEnabled = true;
				LabelHeight.IsEnabled = true;
				break;
			}
			if (ChkBoxUseSizeOfFirstPic.IsChecked == true) {
				TextWidth.IsEnabled = false;
				TextHeight.IsEnabled = false;
				m_useSizeOfFirstPic = true;
			}
			else {
				m_useSizeOfFirstPic = false;
			}
			ProgBarSingle.Value += 10.0;
		}

		private void Window_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effects = DragDropEffects.Move;
			}
		}

		private void UpdateSingleBar(double val) {
			ProgBarSingle.Value = val;
			PorgBarTotal.Value = 1.0 * val / m_totalCnt + m_finishCnt / m_totalCnt * 100.0;
		}

		private async void Window_Drop(object sender, DragEventArgs e) {
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
				return;
			}
			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) {
				return;
			}
			if (paths.Length <= 0) {
				return;
			}

			int pagesizex = 0;
			int pagesizey = 0;
			if (!m_useSizeOfFirstPic) {
				pagesizex = int.Parse(TextWidth.Text);
				pagesizey = int.Parse(TextHeight.Text);
			}

			List<string> files = [];
			List<string> directories = [];
			List<string> unknown = [];
			foreach (var path in paths) {
				if (File.Exists(path)) {
					files.Add(path);
				}
				else if (Directory.Exists(path)) {
					directories.Add(path);
				}
				else {
					unknown.Add(path);
				}
			}

			List<Task> tasks = [];
			m_finishCnt = 0;
			string outputPath;
			if (directories.Count > 0) {
				m_totalCnt = directories.Count;
				if (files.Count > 0) {
					m_totalCnt++;
					outputPath = EnumFileName(Path.GetDirectoryName(files[0]) ?? "", Path.GetFileNameWithoutExtension(files[0]), ".pdf");
					List<string> failed;
					try {
						failed = await PicMergeToPdf.Process.ProcessAsync(outputPath, files, m_pageSizeType, pagesizex, pagesizey);
					}
					finally {
						;
					}
					if (failed.Count > 0)
						tasks.Add(Task.Run(() => {
							string msg = $"以下文件无法加入 \"{outputPath}\"：";
							foreach (var f in failed) {
								msg += "\r\n";
								msg += f;
							}
							MessageBox.Show(msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
						}));
					m_finishCnt++;
				}
				foreach (string dir in directories) {
					List<string> filelist = Directory.EnumerateFiles(dir).ToList();
					filelist.Sort();
					outputPath = EnumFileName(Path.GetDirectoryName(dir) ?? dir, Path.GetFileName(dir), ".pdf");
					List<string> failed = [];
					try {
						failed = await PicMergeToPdf.Process.ProcessAsync(outputPath, filelist, m_pageSizeType, pagesizex, pagesizey);
					}
					catch (Exception) {
						MessageBox.Show($"FUCK! {m_pageSizeType} {dir} {filelist.Count}");
					}
					if (failed.Count > 0)
						tasks.Add(Task.Run(() => {
							string msg = $"以下文件无法加入 \"{outputPath}\"：";
							foreach (var f in failed) {
								msg += "\r\n";
								msg += f;
							}
							MessageBox.Show(msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
						}));
					m_finishCnt++;
				}
			}
			else if (files.Count > 0) {
				m_totalCnt = 1;
				outputPath = EnumFileName(Path.GetDirectoryName(files[0]) ?? "", Path.GetFileNameWithoutExtension(files[0]), ".pdf");
				List<string> failed;
				try {
					failed = await PicMergeToPdf.Process.ProcessAsync(outputPath, files, m_pageSizeType, pagesizex, pagesizey);
				}
				finally {
					;
				}
				if (failed.Count > 0)
					tasks.Add(Task.Run(() => {
						string msg = $"以下文件无法加入 \"{outputPath}\"：";
						foreach (var f in failed) {
							msg += "\r\n";
							msg += f;
						}
						MessageBox.Show(msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
					}));
				m_finishCnt++;
			}

			if (unknown.Count > 0) {
				string msg = "以下内容无法处理：";
				foreach (string str in unknown) {
					msg += "\r\n";
					msg += str;
				}
				MessageBox.Show(this, msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			Task.WaitAll([.. tasks]);
		}

		private static string EnumFileName(string dir, string stem, string exname) {
			string res = Path.Combine(dir, stem + exname);
			int i = 0;
			while (File.Exists(res)) {
				i++;
				res = Path.Combine(dir, $"{stem} ({i}){exname}");
			}
			return res;
		}

	}
}