using System.Globalization;
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
		private readonly Processor m_processor;

		public MainWindow() {
			if (CultureInfo.CurrentCulture.Name.Equals("zh-cn", StringComparison.OrdinalIgnoreCase)) {
				ChangeLang(1);
			}
			else {
				ChangeLang(0);
			}
			InitializeComponent();

			m_processor = new Processor(this, BarSetNum, BarSetFinish); // 不能放上去，因为要用this。

			int index = 0;
			foreach (string paperType in Settings1.Default.Papers.Split(',')) {
				comboBoxPageSize.Items.Insert(index++, new ComboBoxItem() {
					Content = paperType
				});
			}

			textWidth.Text = Settings1.Default.PageSizeWidth.ToString();
			textHeight.Text = Settings1.Default.PageSizeHeight.ToString();

			Settings1.Default.PagePageType = int.Clamp(Settings1.Default.PagePageType, 0, comboBoxPageSize.Items.Count - 1);
			comboBoxPageSize.SelectedIndex = Settings1.Default.PagePageType;

			Settings1.Default.PageIsFixed &= 3;
			radioBtnFixedWidth.IsChecked = (Settings1.Default.PageIsFixed & 1) != 0;
			bool val = (Settings1.Default.PageIsFixed & 2) != 0;
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
					labelTotal.Content = string.Format(App.Current.FindResource("HaveFinishedPercent").ToString() ?? "{0:F2}", ratio);
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
					labelTotal.Content = App.Current.FindResource("Ready").ToString();
					porgBarTotal.Value = 100.0;
				});
			}
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
				return;
			}
			if (e.Key == Key.Decimal && sender is TextBox box && !box.Text.Contains('.')) {
				return; // 允许有一个小数点。
			}
			e.Handled = true;
		}

		/// <summary>
		/// 页面尺寸类型的单选框 改变 的 通知。用来确定m_pageSizeType。
		/// </summary>
		private void BtnPageSize_Changed(object sender, RoutedEventArgs e) {
			int sizeType = 0;

			if (radioBtnFixedWidth.IsChecked == true)
				sizeType |= 1;
			if (radioBtnFixedHeight.IsChecked == true)
				sizeType |= 2;

			comboBoxPageSize.IsEnabled = sizeType != 0;
			if (comboBoxPageSize.IsEnabled && comboBoxPageSize.SelectedIndex == comboBoxPageSize.Items.Count - 1) {
				textWidth.IsEnabled = true;
				textHeight.IsEnabled = true;
			}
			else {
				textWidth.IsEnabled = false;
				textHeight.IsEnabled = false;
			}

			if (!Started)
				return;

			Settings1.Default.PageIsFixed = sizeType;
		}

		private void ComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			if (comboBoxPageSize.SelectedIndex >= 0 && comboBoxPageSize.SelectedIndex == comboBoxPageSize.Items.Count - 1) {
				textWidth.IsEnabled = true;
				textHeight.IsEnabled = true;
				if (!Started)
					return;
				Settings1.Default.PagePageType = comboBoxPageSize.SelectedIndex;
				return;
			}

			if (comboBoxPageSize.SelectedItem is not ComboBoxItem comboBox) {
				MessageBox.Show(this, $"Could not set the page type. ({comboBoxPageSize.SelectedIndex})");
				return;
			}

			System.Drawing.Size size;
			try {
				var obj = Settings1.Default[$"Paper{comboBox.Content}"];
				if (obj is not System.Drawing.Size _size) {
					MessageBox.Show(this, $"Could not load size data of page type \"{comboBox.Content}\".");
					return;
				}
				size = _size;
			}
			catch {
				MessageBox.Show(this, $"Could not load size data of page type \"{comboBox.Content}\".");
				return;
			}
			textWidth.Text = size.Width.ToString();
			textHeight.Text = size.Height.ToString();

			textWidth.IsEnabled = false;
			textHeight.IsEnabled = false;

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

		private void IoCheckedChanged(object sender, RoutedEventArgs e) {
			if (!Started)
				return;
			Settings1.Default.IORecurse = chkBoxRecursion.IsChecked == true;
			Settings1.Default.IOKeepStruct = chkBoxKeepStructure.IsChecked == true;
			Settings1.Default.IOCompress = chkBoxCompressAll.IsChecked == true;
			Settings1.Default.IONoMove = chkBoxStayNoMove.IsChecked == true;
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
		private void Window_Drop(object sender, DragEventArgs e) {
			Activate();
			if (!e.Data.GetDataPresent(DataFormats.FileDrop)) {
				return;
			}
			if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) {
				Task.Run(() => {
					App.Current.Dispatcher.Invoke(() => {
						MessageBox.Show(
							this,
							App.Current.FindResource("InvalidDrop").ToString(),
							$"{Title}: {App.Current.FindResource("Error")}",
							MessageBoxButton.OK,
							MessageBoxImage.Error
						);
					});
				});
				return;
			}
			if (m_processor.IsRunning()) {
				Task.Run(() => {
					App.Current.Dispatcher.Invoke(() => {
						MessageBox.Show(
							this,
							App.Current.FindResource("WaitForCurrentTask").ToString(),
							$"{Title}: {App.Current.FindResource("Error")}",
							MessageBoxButton.OK,
							MessageBoxImage.Error
						);
					});
				});
				return;
			}
			if (paths.Length <= 0) {
				return;
			}
			BarSetNum(0, 1);
			Processor.Parameters param = new(
				_pageFixedType: Settings1.Default.PageIsFixed,
				_pagesizex: Settings1.Default.PageSizeWidth,
				_pagesizey: Settings1.Default.PageSizeHeight,

				_recursion: Settings1.Default.IORecurse,
				_keepStruct: Settings1.Default.IOKeepStruct,
				_compress: Settings1.Default.IOCompress,
				_stayNoMove: Settings1.Default.IONoMove,

				_type: Settings1.Default.CompressType,
				_quality: Settings1.Default.CompressQuality,
				_resize: Settings1.Default.CompressResize,
				_width: Settings1.Default.CompressResizeWidth ? Settings1.Default.CompressResizeWidthValue : 0,
				_height: Settings1.Default.CompressResizeHeight ? Settings1.Default.CompressResizeHeightValue : 0,
				_shortSide: Settings1.Default.CompressResizeShort ? Settings1.Default.CompressResizeShortValue : 0,
				_longSide: Settings1.Default.CompressResizeLong ? Settings1.Default.CompressResizeLongValue : 0
			);
			if (m_processor.Start(paths, param) == false) {
				Task.Run(() => {
					App.Current.Dispatcher.Invoke(() => {
						MessageBox.Show(
							this,
							App.Current.FindResource("WaitForCurrentTask").ToString(),
							$"{Title}: {App.Current.FindResource("Error")}",
							MessageBoxButton.OK,
							MessageBoxImage.Error
						);
					});
				});
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
					App.Current.FindResource("WaitForCurrentTask").ToString(),
					Title,
					MessageBoxButton.OK,
					MessageBoxImage.Information
				);
				e.Cancel = true;
			}
			Settings1.Default.Save();
		}
	}
}