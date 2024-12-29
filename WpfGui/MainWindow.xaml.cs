using iText.Commons.Utils;
using iText.Layout.Element;
using System.IO;
using System.Windows;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		bool m_useSizeOfFirstPic = true;
		int m_pageSizeType = 2;
		int m_totalCnt = 1; // 此次处理将生成多少目标文件。
		int m_finishCnt = 0;
		int m_singleCnt = 1;
		Task? m_lastTask;

		public MainWindow() {
			InitializeComponent();
			ChkBoxUseSizeOfFirstPic.IsChecked = true;
			RadioBtnFixedWidth.IsChecked = true;
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
		}

		private void Window_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effects = DragDropEffects.Move;
			}
		}

		private void UpdateSingleBar(int cnt) {
			App.Current.Dispatcher.Invoke(() => {
				ProgBarSingle.Value = 100.0 * cnt / m_singleCnt;
				PorgBarTotal.Value = 100.0 * (1.0 * cnt / m_singleCnt + m_finishCnt) / m_totalCnt;
				LabelSingle.Content = $"{cnt} / {m_singleCnt}";
				LabelTotal.Content = $"{m_finishCnt} / {m_totalCnt}";
			});
		}

		[System.Runtime.InteropServices.DllImport("Shlwapi.dll", CharSet = System.Runtime.InteropServices.CharSet.Unicode)]
		private static extern int StrCmpLogicalW(string psz1, string psz2);

		private void Window_Drop(object sender, DragEventArgs e) {
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
				return;
			}
			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) {
				MessageBox.Show(this, "拖入的数据不合规。", $"{this.Title}: Error");
				return;
			}
			if (m_lastTask != null && !m_lastTask.IsCompleted) {
				MessageBox.Show(this, "请等待当前任务完成。", $"{Title}: Error");
				return;
			}
			if (paths.Length <= 0) {
				return;
			}
			bool recursion = ChkBoxRecursion.IsChecked != false;
			bool keepStruct = ChkBoxKeepStructure.IsChecked != false;
			bool stayNoMove = ChkBoxStayNoMove.IsChecked == true;
			m_lastTask = Task.Run(() => { Process(paths, recursion, keepStruct, stayNoMove); });
			return;
		}

		private async void Process(string[] paths, bool recursion, bool keepStruct, bool stayNoMove) {
			string destFolder = "";
			if (!stayNoMove) {
				bool? result = false;
				App.Current.Dispatcher.Invoke(() => {
					// Configure open folder dialog box
					Microsoft.Win32.OpenFolderDialog dialog = new() {
						Multiselect = false,
						Title = "选择输出地点",
						DefaultDirectory = Directory.Exists(paths[0]) ? paths[0] : (Path.GetDirectoryName(paths[0]) ?? "")
					};

					// Show open folder dialog box
					result = dialog.ShowDialog(this);

					// Get the selected folder
					destFolder = dialog.FolderName;
				});

				// Process open folder dialog box results
				if (result != true)
					return;
				EnsureFolderExisting(destFolder);
			}
			int pagesizex = 0;
			int pagesizey = 0;
			if (!m_useSizeOfFirstPic) {
				pagesizex = int.Parse(TextWidth.Text);
				pagesizey = int.Parse(TextHeight.Text);
			}

			List<string> files = [];       // 文件列表。
			List<Tuple<string, string>> directories = []; // 文件夹列表，第一个是基准文件夹的绝对路径，第二个是相对路径。
			List<string> unknown = [];     // 无法处理的文件的列表。
			foreach (var path in paths) {  // 遍历拖入的路径。
				if (File.Exists(path)) {   // 是否是文件。
					files.Add(path);
				}
				else if (Directory.Exists(path)) { // 是否是文件夹。
					directories.Add(Tuple.Create(path, ""));
					if (recursion)
						RecursionAllDirectories(path, path, directories);
				}
				else {
					unknown.Add(path); // 加入无法处理的列表。
				}
			}

			List<Task> tasks = [];
			m_finishCnt = 0;
			string outputPath;
			if (directories.Count > 0) { // 拖入的列表中存在目录。
				m_totalCnt = directories.Count;
				if (files.Count > 0) {   // 同时也存在文件。
					m_totalCnt++;
					m_singleCnt = files.Count;
					outputPath = EnumFileName(
						stayNoMove ? (Path.GetDirectoryName(files[0]) ?? "") : destFolder,
						Path.GetFileNameWithoutExtension(files[0]),
						".pdf"
					);
					files.Sort(StrCmpLogicalW);
					List<string> failed;
					try {
						failed = await PicMergeToPdf.Process.ProcessAsync(outputPath, files, m_pageSizeType, pagesizex, pagesizey);
					}
					catch (Exception ex) {
						failed = ["处理过程出现异常", ex.Message];
					}
					if (failed.Count > 0)
						tasks.Add(Task.Run(() => {
							string msg = $"以下文件无法加入 \"{outputPath}\"：";
							for (int i = 0, n = failed.Count; i + 1 < n; i += 2) {
								msg += "\r\n";
								msg += failed[i];
								msg += ": ";
								msg += failed[i + 1];
							}
							App.Current.Dispatcher.Invoke(() => {
								MessageBox.Show(this, msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
							});
						}));
					m_finishCnt++;
				}
				//directories.Sort(StrCmpLogicalW);
				foreach (Tuple<string, string> pair in directories) {
					string dir = Path.Combine(pair.Item1, pair.Item2);
					var fileList = Directory.EnumerateFiles(dir);
					if (!fileList.Any()) { // 跳过空文件夹
						m_finishCnt++;
						continue;
					}
					List<string> filelist = fileList.ToList();
					filelist.Sort(StrCmpLogicalW);
					m_singleCnt = filelist.Count;

					string dp;
					if (stayNoMove) {
						dp = Path.GetDirectoryName(dir) ?? dir;
					}
					else if (keepStruct) {
						dp = Path.Combine(destFolder, pair.Item2);
						if (!string.IsNullOrEmpty(pair.Item2))
							dp = Path.GetDirectoryName(dp) ?? dp;
						EnsureFolderExisting(dp);
					}
					else {
						dp = destFolder;
					}

					outputPath = EnumFileName(dp, Path.GetFileName(dir), ".pdf");

					List<string> failed;
					try {
						failed = await PicMergeToPdf.Process.ProcessAsync(outputPath, filelist, m_pageSizeType, pagesizex, pagesizey, pair.Item2);
					}
					catch (Exception ex) {
						failed = ["处理过程出现异常", ex.Message];
					}
					if (failed.Count > 0)
						tasks.Add(Task.Run(() => {
							string msg = $"以下文件无法加入 \"{outputPath}\"：";
							for (int i = 0, n = failed.Count; i + 1 < n; i += 2) {
								msg += "\r\n";
								msg += failed[i];
								msg += ": ";
								msg += failed[i + 1];
							}
							App.Current.Dispatcher.Invoke(() => {
								MessageBox.Show(this, msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
							});
						}));
					m_finishCnt++;
				}
			}
			else if (files.Count > 0) { // 拖入的列表只有文件。
				m_totalCnt = 1;
				m_singleCnt = files.Count;
				files.Sort(StrCmpLogicalW);
				outputPath = EnumFileName(
					stayNoMove ? (Path.GetDirectoryName(files[0]) ?? "") : destFolder,
					Path.GetFileNameWithoutExtension(files[0]),
					".pdf"
				);
				List<string> failed;
				try {
					failed = await PicMergeToPdf.Process.ProcessAsync(outputPath, files, m_pageSizeType, pagesizex, pagesizey);
				}
				catch (Exception ex) {
					failed = ["处理过程出现异常", ex.Message];
				}
				if (failed.Count > 0)
					tasks.Add(Task.Run(() => {
						string msg = $"以下文件无法加入 \"{outputPath}\"：";
						for (int i = 0, n = failed.Count; i + 1 < n; i += 2) {
							msg += "\r\n";
							msg += failed[i];
							msg += ": ";
							msg += failed[i + 1];
						}
						App.Current.Dispatcher.Invoke(() => {
							MessageBox.Show(this, msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
						});
					}));
				m_finishCnt++;
			}
			UpdateSingleBar(m_singleCnt);
			App.Current.Dispatcher.Invoke(() => {
				LabelSingle.Content = "就绪";
				LabelTotal.Content = "就绪";
			});

			if (unknown.Count > 0) {
				string msg = "以下内容无法处理：";
				foreach (string str in unknown) {
					msg += "\r\n";
					msg += str;
				}
				App.Current.Dispatcher.Invoke(() => {
					MessageBox.Show(this, msg, "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
				});
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

		private static void RecursionAllDirectories(string dir, string basedir, List<Tuple<string, string>> list) {
			foreach (string d in Directory.GetDirectories(dir)) {
				list.Add(Tuple.Create(basedir, Path.GetRelativePath(basedir, d)));
				RecursionAllDirectories(d, basedir, list);
			}
			return;
		}

		private static void EnsureFolderExisting(string path) {
			if (Directory.Exists(path))
				return;
			string parent = Path.GetDirectoryName(path) ??
				throw new DirectoryNotFoundException($"Parent of \"{path}\" is not exist!");
			EnsureFolderExisting(parent);
			Directory.CreateDirectory(path);
			return;
		}

	}
}