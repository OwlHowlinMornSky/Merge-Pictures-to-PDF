using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		/// <summary>
		/// 用来 lock 进度条和标签 的 对象。
		/// </summary>
		private readonly Lock m_lockBar = new();
		/// <summary>
		/// 处理拖入数据 的 对象。
		/// </summary>
		private readonly PicMerge.Processor m_processor;

		public MainWindow() {
			InitializeComponent();

			m_processor = new PicMerge.Processor(BarSetNum, BarSetFinish, PopWarning);
		}

		/// <summary>
		/// 用于 设置进度条进度 的 回调目标。
		/// </summary>
		/// <param name="i">分子</param>
		/// <param name="n">分母</param>
		private void BarSetNum(int i, int n) {
			lock (m_lockBar) {
				double ratio = 1.0 * i / n;
				App.Current.Dispatcher.Invoke(() => {
					TaskbarItemInfo.ProgressValue = ratio;
					labelTotal.Content = string.Format(App.Current.TryFindResource("HaveFinishedPercent").ToString() ?? "{0:F2}", ratio * 100.0);
					porgBarTotal.Value = ratio * 100.0;
				});
			}
		}

		/// <summary>
		/// 用来 设置任务完成 的 回调目标。
		/// </summary>
		private void BarSetFinish() {
			lock (m_lockBar) {
				App.Current.Dispatcher.Invoke(() => {
					TaskbarItemInfo.ProgressValue = 1.0;
					labelTotal.Content = App.Current.TryFindResource("Ready").ToString();
					porgBarTotal.Value = 100.0;
				});
			}
		}

		private void PopWarning(string logPath) {
			App.Current.Dispatcher.Invoke(() => {
				TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Error;
				Activate();
				var choice = MessageBox.Show(
					this,
					string.Format(
						App.Current.TryFindResource("CannotProcess") as string
						?? "Failed files in log: {0}" + Environment.NewLine + "Open it?",
						logPath
						),
					$"{Title}: {App.Current.TryFindResource("Warning")}",
					MessageBoxButton.YesNo,
					MessageBoxImage.Warning,
					MessageBoxResult.Yes
				);
				if (choice == MessageBoxResult.Yes) {
					OpenFolderAndSelectFile(logPath);
				}
			});
		}

		private void ButtonMorePreferences_Click(object sender, RoutedEventArgs e) {
			WindowMorePreferences dialog = new() {
				Owner = this
			};
			var reset = dialog.ShowDialog();
			if (reset is true && DataContext is DataMain) {
				var dm = new DataMain();
				DataContext = dm;
			}
		}

		/// <summary>
		/// 拖入的通知。只接受文件。
		/// </summary>
		private void Window_DragEnter(object sender, DragEventArgs e) {
			if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
				e.Effects = DragDropEffects.Move;
			}
		}

		/// <summary>
		/// 拖放的通知。只接收文件。交予Processor处理。不能 同时有两次拖放在处理。
		/// </summary>
		private async void Window_Drop(object sender, DragEventArgs e) {
			Activate();
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
				return;
			}
			if (Validation.GetHasError(doubleboxWidth) ||
				Validation.GetHasError(doubleboxHeight) ||
				Validation.GetHasError(doubleboxPPI)) {
				await PopErrorAsync(App.Current.TryFindResource("InvalidParams").ToString() ?? "InvalidParams");
				return;
			}
			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length <= 0) {
				await PopErrorAsync(App.Current.TryFindResource("InvalidDrop").ToString() ?? "InvalidDrop");
				return;
			}
			if (m_processor.IsRunning()) {
				await PopErrorAsync(App.Current.TryFindResource("WaitForCurrentTask").ToString() ?? "WaitForCurrentTask");
				return;
			}
			BarSetNum(0, 1);

			PicMerge.IOParam ioParam;
			string destDir = "";
			string moveDestDir = "";
			if (!Settings1.Default.IONoMove) {
				var res = await AskForDestinationAsync(
					string.IsNullOrEmpty(Settings1.Default.PrevSelectTargetPath) ?
					paths[0] : Settings1.Default.PrevSelectTargetPath
				);
				if (res == null)
					return;
				Settings1.Default.PrevSelectTargetPath = res;
				destDir = res;
			}
			if (Settings1.Default.IOMoveProcessed) {
				var res = await AskForDestinationAsync(
					string.IsNullOrEmpty(Settings1.Default.PrevSelectMovePath) ?
					paths[0] : Settings1.Default.PrevSelectMovePath,
					true
				);
				if (res == null)
					return;
				Settings1.Default.PrevSelectMovePath = res;
				moveDestDir = res;
			}
			ioParam = new(
				_recursion: Settings1.Default.IORecurse,
				_keepStruct: Settings1.Default.IOKeepStruct,
				_stayNoMove: Settings1.Default.IONoMove,
				_targetPath: destDir,
				_moveProcessed: Settings1.Default.IOMoveProcessed,
				_moveDest: moveDestDir,
				_pdfInFolder: Settings1.Default.IOPdfInFolder
			);
			PicMerge.PageParam pageParam = new(
				_fixedType:
				(Settings1.Default.PageFixedHeight ? PicMerge.PageParam.FixedType.HeightFixed : 0) |
				(Settings1.Default.PageFixedWidth ? PicMerge.PageParam.FixedType.WidthFixed : 0),
				_width: Settings1.Default.PageSizeWidth,
				_height: Settings1.Default.PageSizeHeight,
				_dpi: Settings1.Default.PageDpi
			);
			PicMerge.ImageParam imageParam = new(
				_compress: Settings1.Default.IOCompress,
				_format: ImageFormatHelp.StringToIndex(Settings1.Default.CompressFormat),
				_quality: Settings1.Default.CompressQuality,
				_resize: Settings1.Default.CompressResize,
				_width: Settings1.Default.CompressResizeWidth ? Settings1.Default.CompressResizeWidthValue : 0,
				_height: Settings1.Default.CompressResizeHeight ? Settings1.Default.CompressResizeHeightValue : 0,
				_shortSide: Settings1.Default.CompressResizeShort ? Settings1.Default.CompressResizeShortValue : 0,
				_longSide: Settings1.Default.CompressResizeLong ? Settings1.Default.CompressResizeLongValue : 0,
				_reduceBy2: Settings1.Default.CompressResizeReduceByPow2
			);
			TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.Normal;
			bool succeed = await m_processor.StartAsync(paths, pageParam, imageParam, ioParam);
			if (!succeed) {
				await PopErrorAsync(App.Current.TryFindResource("WaitForCurrentTask").ToString() ?? "Error.");
			}
			TaskbarItemInfo.ProgressState = System.Windows.Shell.TaskbarItemProgressState.None;
			TaskbarItemInfo.ProgressValue = 0.0;
			return;
		}

		/// <summary>
		/// 即将关闭窗口的通知。由于主线程必须等待Task处理结束，所以任务进行时不能关闭。
		/// </summary>
		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			if (m_processor.IsRunning()) {
				MessageBox.Show(
					this,
					App.Current.TryFindResource("WaitForCurrentTask").ToString(),
					Title,
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
				e.Cancel = true;
			}
			Settings1.Default.Save();
		}

		/// <summary>
		/// 询问目标目录。
		/// </summary>
		/// <param name="defpath">初始目录</param>
		/// <returns>选择的目录，或者 null 表示取消</returns>
		private async Task<string?> AskForDestinationAsync(string defpath, bool isMoveTo = false) {
			return await Task.Run(() => {
				string? res = null;
				App.Current.Dispatcher.Invoke(() => {
					// Configure open folder dialog box
					Microsoft.Win32.OpenFolderDialog dialog = new() {
						Multiselect = false,
						Title = $"{Title}: {App.Current.TryFindResource(isMoveTo ? "ChooseMoveDestinationDir" : "ChooseDestinationDir").ToString() ?? "output location"}",
						InitialDirectory = Directory.Exists(defpath) ? defpath : Path.GetDirectoryName(defpath)
					};

					// Show open folder dialog box
					// Process open folder dialog box results
					if (dialog.ShowDialog(this) == true) {
						// Get the selected folder
						res = dialog.FolderName;
					}
				});
				return res;
			});
		}

		internal async Task PopErrorAsync(string msg) {
			await Task.Run(() => {
				App.Current.Dispatcher.Invoke(() => {
					MessageBox.Show(
						this, msg,
						$"{Title}: {App.Current.TryFindResource("Error")}",
						MessageBoxButton.OK,
						MessageBoxImage.Error
					);
				});
			});
		}


		[LibraryImport("shell32.dll")]
		private static partial void ILFree(IntPtr pidlList);

		[LibraryImport("shell32.dll", StringMarshalling = StringMarshalling.Utf16)]
		private static partial IntPtr ILCreateFromPathW(string pszPath);

		[LibraryImport("shell32.dll")]
		private static partial int SHOpenFolderAndSelectItems(IntPtr pidlList, uint cild, IntPtr children, uint dwFlags);

		private static void OpenFolderAndSelectFile(string filePath) {
			// 确保文件路径是绝对路径
			filePath = Path.GetFullPath(filePath);

			// 获取文件的 PIDL (Pointer to an Item ID List)
			IntPtr pidlList = ILCreateFromPathW(filePath);

			if (pidlList != IntPtr.Zero) {
				try {
					// 调用 API 打开资源管理器并选中文件
					// 第三个参数为 IntPtr.Zero 表示只选中一个文件
					var _ = SHOpenFolderAndSelectItems(pidlList, 0, IntPtr.Zero, 0);
				}
				finally {
					// 释放 PIDL 资源
					ILFree(pidlList);
				}
			}
		}

		private void Doublebox_ValueChanged(object sender, TextChangedEventArgs e) {
			if (DataContext is DataMain dm) {
				dm.PageSizeId = 0;
			}
		}
	}
}