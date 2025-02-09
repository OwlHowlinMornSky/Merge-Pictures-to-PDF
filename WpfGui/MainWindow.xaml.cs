using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		protected bool Started {
			get; private set;
		} = false;
		/// <summary>
		/// 用来 lock 进度条和标签 的 对象。
		/// </summary>
		private readonly object m_lockBar = new();
		/// <summary>
		/// 处理拖入数据 的 对象。
		/// </summary>
		private readonly PicMerge.Processor m_processor;

		public MainWindow() {
			if (CultureInfo.CurrentCulture.Name.Equals("zh-cn", StringComparison.OrdinalIgnoreCase)) {
				ChangeLang(1);
			}
			else {
				ChangeLang(0);
			}
			InitializeComponent();

			m_processor = new PicMerge.Processor(BarSetNum, BarSetFinish, PopWarning);

			int index = 0;
			foreach (string paperType in Settings1.Default.Papers.Split(',')) {
				comboBoxPageSize.Items.Insert(index++, new ComboBoxItem() {
					Content = paperType
				});
			}

			textWidth.Text = Settings1.Default.PageSizeWidth.ToString();
			textHeight.Text = Settings1.Default.PageSizeHeight.ToString();
			textDpi.Text = Settings1.Default.PageDpi.ToString();

			Settings1.Default.PagePageType = int.Clamp(Settings1.Default.PagePageType, 0, comboBoxPageSize.Items.Count - 1);
			comboBoxPageSize.SelectedIndex = Settings1.Default.PagePageType;

			radioBtnFixedWidth.IsChecked = Settings1.Default.PageFixedWidth;
			bool val = Settings1.Default.PageFixedHeight;
			radioBtnFixedHeight.IsChecked = !val;
			radioBtnFixedHeight.IsChecked = val; // This is to trigger event.

			chkBoxRecursion.IsChecked = Settings1.Default.IORecurse;
			chkBoxKeepStructure.IsChecked = Settings1.Default.IOKeepStruct;
			chkBoxCompressAll.IsChecked = Settings1.Default.IOCompress;
			chkBoxStayNoMove.IsChecked = Settings1.Default.IONoMove;

			Started = true;
		}

		/// <summary>
		/// 用于 设置进度条进度 的 回调目标。
		/// </summary>
		/// <param name="i">分子</param>
		/// <param name="n">分母</param>
		private void BarSetNum(int i, int n) {
			lock (m_lockBar) {
				double ratio = 100.0 * i / n;
				App.Current.Dispatcher.Invoke(() => {
					labelTotal.Content = string.Format(App.Current.TryFindResource("HaveFinishedPercent").ToString() ?? "{0:F2}", ratio);
					porgBarTotal.Value = ratio;
				});
			}
		}

		/// <summary>
		/// 用来 设置任务完成 的 回调目标。
		/// </summary>
		private void BarSetFinish() {
			lock (m_lockBar) {
				App.Current.Dispatcher.Invoke(() => {
					labelTotal.Content = App.Current.TryFindResource("Ready").ToString();
					porgBarTotal.Value = 100.0;
				});
			}
		}

		private void PopWarning(string logPath) {
			App.Current.Dispatcher.BeginInvoke(() => {
				MessageBox.Show(
					this,
					string.Format(App.Current.TryFindResource("CannotProcess") as string ?? "Failed files in log: {0}", logPath),
					$"{Title}: {App.Current.TryFindResource("Warning")}",
					MessageBoxButton.OK,
					MessageBoxImage.Warning
				);
			});
		}

		/// <summary>
		/// 更改语言。
		/// Change Language.
		/// </summary>
		/// <param name="index">default: English, 1: Chinese(S)</param>
		internal static void ChangeLang(int index) {
			ResourceDictionary rd = new() {
				Source = index switch {
					1 => new Uri("DictionaryMainGUI.zh-CN.xaml", UriKind.Relative),
					_ => new Uri("DictionaryMainGUI.xaml", UriKind.Relative),
				}
			};
			App.Current.Resources.MergedDictionaries.Clear();
			App.Current.Resources.MergedDictionaries.Add(rd);
			return;
		}

		/// <summary>
		/// 输入尺寸的框 的 键入通知。用来限制 只能输入数字。
		/// </summary>
		private void TextNum_PreviewKeyDown(object sender, KeyEventArgs e) {
			bool isNum = e.Key >= Key.D0 && e.Key <= Key.D9;
			bool isNumPad = e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9;
			bool isControl = e.Key == Key.Back || e.Key == Key.Enter || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right;
			if (isNum || isNumPad || isControl) {
				comboBoxPageSize.SelectedIndex = comboBoxPageSize.Items.Count - 1; // 改为自定义。
				return;
			}
			if (e.Key == Key.Decimal && sender is TextBox box && !box.Text.Contains('.')) {
				comboBoxPageSize.SelectedIndex = comboBoxPageSize.Items.Count - 1; // 改为自定义。
				return; // 允许有一个小数点。
			}
			e.Handled = true;
		}

		private void TextNum_PreviewKeyDown_Int(object sender, KeyEventArgs e) {
			bool isNum = e.Key >= Key.D0 && e.Key <= Key.D9;
			bool isNumPad = e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9;
			bool isControl = e.Key == Key.Back || e.Key == Key.Enter || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right;
			if (isNum || isNumPad || isControl) {
				comboBoxPageSize.SelectedIndex = comboBoxPageSize.Items.Count - 1; // 改为自定义。
				return;
			}
			e.Handled = true;
		}

		/// <summary>
		/// 页面尺寸类型的单选框 改变 的 通知。用来确定m_pageSizeType。
		/// </summary>
		private void BtnPageSize_Changed(object sender, RoutedEventArgs e) {
			comboBoxPageSize.IsEnabled = (radioBtnFixedWidth.IsChecked ?? false) || (radioBtnFixedHeight.IsChecked ?? false);
			textWidth.IsEnabled = (radioBtnFixedWidth.IsChecked ?? false) || (radioBtnFixedHeight.IsChecked ?? false);
			textHeight.IsEnabled = (radioBtnFixedWidth.IsChecked ?? false) || (radioBtnFixedHeight.IsChecked ?? false);

			if (!Started)
				return;

			Settings1.Default.PageFixedWidth = radioBtnFixedWidth.IsChecked ?? false;
			Settings1.Default.PageFixedHeight = radioBtnFixedHeight.IsChecked ?? false;
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (comboBoxPageSize.SelectedItem is not ComboBoxItem boxItem) {
				MessageBox.Show(this, $"Could not set the page type. ({comboBoxPageSize.SelectedIndex})");
				return;
			}

			if (comboBoxPageSize.SelectedIndex < comboBoxPageSize.Items.Count - 1) {
				System.Drawing.Size size;
				try {
					var obj = Settings1.Default[$"Paper{boxItem.Content}"];
					if (obj is not System.Drawing.Size _size) {
						MessageBox.Show(this, $"Could not load size data of page type \"{boxItem.Content}\".");
						return;
					}
					size = _size;
				}
				catch {
					MessageBox.Show(this, $"Could not load size data of page type \"{boxItem.Content}\".");
					return;
				}
				textWidth.Text = size.Width.ToString();
				textHeight.Text = size.Height.ToString();
				textDpi.Text = "72";
			}

			if (!Started)
				return;
			Settings1.Default.PagePageType = comboBoxPageSize.SelectedIndex;
		}

		private void PageSizeTextChangedW(object sender, TextChangedEventArgs e) {
			Settings1.Default.PageSizeWidth = float.TryParse(textWidth.Text, out float res) ? res : 0;
		}

		private void PageSizeTextChangedH(object sender, TextChangedEventArgs e) {
			Settings1.Default.PageSizeHeight = float.TryParse(textHeight.Text, out float res) ? res : 0;
		}

		private void PageDpiTextChanged(object sender, TextChangedEventArgs e) {
			Settings1.Default.PageDpi = uint.TryParse(textDpi.Text, out uint res) ? res : 0;
		}

		private void IoCheckedChanged(object sender, RoutedEventArgs e) {
			if (!Started)
				return;
			Settings1.Default.IORecurse = chkBoxRecursion.IsChecked ?? false;
			Settings1.Default.IOKeepStruct = chkBoxKeepStructure.IsChecked ?? false;
			Settings1.Default.IOCompress = chkBoxCompressAll.IsChecked ?? false;
			Settings1.Default.IONoMove = chkBoxStayNoMove.IsChecked ?? false;
		}

		private void Button_Click(object sender, RoutedEventArgs e) {
			WindowMorePreferences dialog = new() {
				Owner = this
			};
			dialog.ShowDialog();
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
			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths || paths.Length <= 0) {
				await PopErrorAsync(App.Current.TryFindResource("InvalidDrop").ToString() ?? "Error.");
				return;
			}
			if (m_processor.IsRunning()) {
				await PopErrorAsync(App.Current.TryFindResource("WaitForCurrentTask").ToString() ?? "Error.");
				return;
			}
			BarSetNum(0, 1);

			PicMerge.IOParam ioParam;
			string destDir = "";
			if (!Settings1.Default.IONoMove) {
				var res = await AskForDestinationAsync(
					string.IsNullOrEmpty(Settings1.Default.PrevSelectTargetPath) ?
					paths[0] :
					Settings1.Default.PrevSelectTargetPath
				);
				if (res == null)
					return;
				Settings1.Default.PrevSelectTargetPath = res;
				destDir = res;
			}
			ioParam = new(
				_recursion: Settings1.Default.IORecurse,
				_keepStruct: Settings1.Default.IOKeepStruct,
				_stayNoMove: Settings1.Default.IONoMove,
				_targetPath: destDir
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
				_format: Settings1.Default.CompressFormat,
				_quality: Settings1.Default.CompressQuality,
				_resize: Settings1.Default.CompressResize,
				_width: Settings1.Default.CompressResizeWidth ? Settings1.Default.CompressResizeWidthValue : 0,
				_height: Settings1.Default.CompressResizeHeight ? Settings1.Default.CompressResizeHeightValue : 0,
				_shortSide: Settings1.Default.CompressResizeShort ? Settings1.Default.CompressResizeShortValue : 0,
				_longSide: Settings1.Default.CompressResizeLong ? Settings1.Default.CompressResizeLongValue : 0,
				_reduceBy2: Settings1.Default.CompressResizeReduceByPow2
			);
			bool succeed = await m_processor.StartAsync(paths, pageParam, imageParam, ioParam);
			if (!succeed) {
				await PopErrorAsync(App.Current.TryFindResource("WaitForCurrentTask").ToString() ?? "Error.");
			}
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
		private async Task<string?> AskForDestinationAsync(string defpath) {
			return await Task.Run(() => {
				string? res = null;
				App.Current.Dispatcher.Invoke(() => {
					// Configure open folder dialog box
					Microsoft.Win32.OpenFolderDialog dialog = new() {
						Multiselect = false,
						Title = $"{Title}: {App.Current.TryFindResource("ChooseDestinationDir").ToString() ?? "output location"}",
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

		private async Task PopErrorAsync(string msg) {
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
	}
}