using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private bool m_useSizeOfFirstPic = true;
		private int m_pageSizeType = 2;

		private Task? m_lastTask;

		private int m_totalCnt = 1; // 此次处理将生成多少目标文件。
		private readonly object m_finishCntMutex = new();
		private int m_finishCnt = 0;

		public MainWindow() {
			InitializeComponent();
			RadioBtnFixedWidth.IsChecked = true;

			PicMerge.Main.SingleUpdate += UpdateSingle;
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

		private void UpdateSingle() {
			lock (m_finishCntMutex) {
				m_finishCnt++;
			}
			UpdateBar();
		}

		private void UpdateBar() {
			int fin = 0;
			lock (m_finishCntMutex) {
				fin = m_finishCnt;
			}
			double ratio = 100.0 * fin / m_totalCnt;
			App.Current.Dispatcher.Invoke(() => {
				LabelTotal.Content = $"{ratio:F1}%";
				PorgBarTotal.Value = ratio;
			});
		}

		[LibraryImport("Shlwapi.dll", EntryPoint = "StrCmpLogicalW", StringMarshalling = StringMarshalling.Utf16)]
		[return: MarshalAs(UnmanagedType.I4)]
		private static partial int StrCmpLogicalW(string psz1, string psz2);

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
			bool compress = ChkBoxCompressAll.IsChecked != false;
			bool stayNoMove = ChkBoxStayNoMove.IsChecked == true;
			int pagesizex = 0;
			int pagesizey = 0;
			if (!m_useSizeOfFirstPic) {
				pagesizex = int.Parse(TextWidth.Text);
				pagesizey = int.Parse(TextHeight.Text);
			}
			m_totalCnt = 1;
			m_finishCnt = 0;
			UpdateBar();
			m_lastTask = Task.Run(() => { Process(paths, recursion, keepStruct, stayNoMove, compress, m_pageSizeType, pagesizex, pagesizey); });
			return;
		}

		private void Process(string[] paths, bool recursion, bool keepStruct, bool stayNoMove, bool compress, int pageSizeType, int pagesizex, int pagesizey) {
			string? destFolder = null;
			m_finishCnt = 0;
			m_totalCnt = 0;
			List<string> files = [];       // 文件列表。
			List<Tuple<string, string>> directories = []; // 文件夹列表，第一个是基准文件夹的绝对路径，第二个是相对路径。
			List<string> unknown = [];     // 无法处理的文件的列表。

			{
				List<Task<int>> prepare = [];

				prepare.Add(Task.Run(() => {
					return ScanInput(paths, recursion, ref files, ref directories, ref unknown);
				}));
				if (!stayNoMove) {
					prepare.Add(Task.Run(() => {
						destFolder = AskForDestation(paths[0]);
						return 0;
					}));
				}
				else {
					destFolder = "";
				}

				Task.WaitAll([.. prepare]);

				m_totalCnt = prepare[0].Result;
			}

			if (destFolder == null || m_totalCnt < 1)
				return;

			UpdateBar();

			List<Task> tasks = [];

			if (files.Count > 0) { // 拖入的列表中存在文件。
				string outputPath = EnumFileName(
					stayNoMove ? (Path.GetDirectoryName(files[0]) ?? "") : destFolder,
					Path.GetFileNameWithoutExtension(files[0]),
					".pdf"
				);
				tasks.Add(
					Task.Run(
						() => {
							ProcessSingleFiles(
								files,
								outputPath, compress,
								pageSizeType, pagesizex, pagesizey,
								Path.GetDirectoryName(files[0]) ?? ""
							);
						}
					)
				);
			}
			if (directories.Count > 0) { // 拖入的列表中存在目录。
				foreach (Tuple<string, string> pair in directories) {
					string sourceDir = Path.Combine(pair.Item1, pair.Item2);
					string destDir;
					if (stayNoMove) {
						destDir = Path.GetDirectoryName(sourceDir) ?? sourceDir;
					}
					else if (keepStruct) {
						destDir = Path.Combine(destFolder, pair.Item2);
						if (!string.IsNullOrEmpty(pair.Item2))
							destDir = Path.GetDirectoryName(destDir) ?? destDir;
						EnsureFolderExisting(destDir);
					}
					else {
						destDir = destFolder;
					}
					string outputPath = EnumFileName(destDir, Path.GetFileName(sourceDir), ".pdf");

					tasks.Add(
						Task.Run(
							() => {
								ProcessOneFolder(
									Path.Combine(pair.Item1, pair.Item2),
									outputPath, compress,
									pageSizeType, pagesizex, pagesizey,
									string.IsNullOrEmpty(pair.Item2) ? Path.GetFileName(pair.Item1) : pair.Item2
								);
							}
						)
					);
				}
			}
			if (unknown.Count > 0) {
				string msg = "以下内容无法处理：";
				foreach (string str in unknown) {
					msg += "\r\n";
					msg += str;
				}
				App.Current.Dispatcher.Invoke(() => {
					MessageBox.Show(this, msg, $"{Title}: 警告", MessageBoxButton.OK, MessageBoxImage.Warning);
				});
			}
			Task.WaitAll([.. tasks]);

			App.Current.Dispatcher.Invoke(() => {
				LabelTotal.Content = "就绪";
			});
		}

		private static int ScanInput(string[] paths, bool recursion, ref List<string> files, ref List<Tuple<string, string>> directories, ref List<string> unknown) {
			int cnt = 0;
			foreach (var path in paths) {  // 遍历拖入的路径。
				if (File.Exists(path)) {   // 是否是文件。
					files.Add(path);
				}
				else if (Directory.Exists(path)) { // 是否是文件夹。
					int dirfilecnt = Directory.EnumerateFiles(path).Count();
					if (dirfilecnt > 0) {
						directories.Add(Tuple.Create(path, ""));
						cnt += dirfilecnt;
					}
					if (recursion)
						cnt += RecursionAllDirectories(path, path, directories);
				}
				else {
					unknown.Add(path); // 加入无法处理的列表。
				}
			}
			return cnt;
		}

		private string? AskForDestation(string defpath) {
			string res = "";

			bool? result = false;
			App.Current.Dispatcher.Invoke(() => {
				// Configure open folder dialog box
				Microsoft.Win32.OpenFolderDialog dialog = new() {
					Multiselect = false,
					Title = "选择输出地点",
					DefaultDirectory = Directory.Exists(defpath) ? defpath : (Path.GetDirectoryName(defpath) ?? "")
				};

				// Show open folder dialog box
				result = dialog.ShowDialog(this);

				// Get the selected folder
				res = dialog.FolderName;
			});

			// Process open folder dialog box results
			if (result != true)
				return null;

			EnsureFolderExisting(res);
			return res;
		}

		private void ProcessOneFolder(string sourceDir, string outputPath, bool compress, int pageSizeType, int pagesizex, int pagesizey, string Title) {
			var fileList = Directory.EnumerateFiles(sourceDir);

			List<string> filelist = fileList.ToList();
			filelist.Sort(StrCmpLogicalW);

			List<string> failed;
			try {
				failed = PicMerge.Main.Process(outputPath, filelist, pageSizeType, pagesizex, pagesizey, compress, Title);
			}
			catch (Exception ex) {
				failed = ["处理过程出现异常", ex.Message];
			}
			if (failed.Count > 0) {
				string msg = $"以下文件无法加入《{Title}》：\r\n";
				foreach (var str in failed) {
					msg += str;
					msg += ".\r\n";
				}
				App.Current.Dispatcher.Invoke(() => {
					MessageBox.Show(this, msg, $"{Title}: 警告", MessageBoxButton.OK, MessageBoxImage.Warning);
				});
			}
		}

		private void ProcessSingleFiles(List<string> files, string outputPath, bool compress, int pageSizeType, int pagesizex, int pagesizey, string Title) {
			files.Sort(StrCmpLogicalW);
			List<string> failed;
			try {
				failed = PicMerge.Main.Process(outputPath, files, pageSizeType, pagesizex, pagesizey, compress, Title);
			}
			catch (Exception ex) {
				failed = ["处理过程出现异常", ex.Message];
			}
			if (failed.Count > 0) {
				string msg = $"以下文件无法加入 \"零散文件\" ：\r\n";
				foreach (var str in failed) {
					msg += str;
					msg += ".\r\n";
				}
				App.Current.Dispatcher.Invoke(() => {
					MessageBox.Show(this, msg, $"{Title}: 警告", MessageBoxButton.OK, MessageBoxImage.Warning);
				});
			}
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

		private static int RecursionAllDirectories(string dir, string basedir, List<Tuple<string, string>> list) {
			int cnt = 0;
			foreach (string d in Directory.EnumerateDirectories(dir)) {
				int dirfilecnt = Directory.EnumerateFiles(d).Count();
				if (dirfilecnt > 0) {
					list.Add(Tuple.Create(basedir, Path.GetRelativePath(basedir, d)));
					cnt += dirfilecnt;
				}
				RecursionAllDirectories(d, basedir, list);
			}
			return cnt;
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

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (m_lastTask != null && !m_lastTask.IsCompleted) {
				MessageBox.Show(this, "请等待任务完成。", $"{Title}");
				e.Cancel = true;
			}
		}
	}
}