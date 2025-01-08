using System.Globalization;
using System.Windows;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		/// <summary>
		/// 页面大小类型，与GUI对应：1是与每张图片一致，2是固定宽度，3是固定大小。
		/// </summary>
		private int m_pageSizeType = 2;
		/// <summary>
		/// 是否 使用第一张图片的尺寸数据。
		/// </summary>
		private bool m_useSizeOfFirstPic = true;

		/// <summary>
		/// 用来 lock 进度条和标签 的 对象。
		/// </summary>
		private readonly object m_lockBar = new();
		/// <summary>
		/// 处理拖入数据 的 对象。
		/// </summary>
		private readonly Processor m_processor;
#if DEBUG
		private int m_lang_test = 1;
#endif

		private int m_type = 1;
		private int m_quality = 80;
		private bool m_archive = true;

		public MainWindow() {
			if (CultureInfo.CurrentCulture.Name.Equals("zh-cn", StringComparison.OrdinalIgnoreCase)) {
				ChangeLang(1);
			}
			else {
				ChangeLang(0);
			}
			InitializeComponent();
			RadioBtnFixedWidth.IsChecked = true; // 默认固定宽度。不能在xaml里check，因为回调函数会访问其他还没初始化的控件。
			m_processor = new Processor(this, BarSetNum, BarSetFinish); // 不能放上去，因为要用this。
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
					LabelTotal.Content = string.Format(App.Current.FindResource("HaveFinishedPercent").ToString() ?? "{0:F2}", ratio);
					PorgBarTotal.Value = ratio;
				});
			}
		}

		/// <summary>
		/// 用来 设置任务完成 的 回调目标。
		/// </summary>
		private void BarSetFinish() {
			lock (m_lockBar) {
				App.Current.Dispatcher.Invoke(() => {
					LabelTotal.Content = App.Current.FindResource("Ready").ToString();
					PorgBarTotal.Value = 100.0;
				});
			}
		}

		/// <summary>
		/// 输入尺寸的框 的 键入通知。用来限制 只能输入数字。
		/// </summary>
		private void TextNum_PreviewKeyDown(object sender, KeyEventArgs e) {
			bool isNum = e.Key >= Key.D0 && e.Key <= Key.D9;
			bool isNumPad = e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9;
			bool isControl = e.Key == Key.Back || e.Key == Key.Enter || e.Key == Key.Delete || e.Key == Key.Left || e.Key == Key.Right || e.Key == Key.Decimal;
			if (isNum || isNumPad || isControl) {
				return;
			}
			e.Handled = true;
		}

		/// <summary>
		/// 页面尺寸类型的单选框 改变 的 通知。用来确定m_pageSizeType。
		/// </summary>
		private void BtnPageSize_Changed(object sender, RoutedEventArgs e) {
#if DEBUG
			ChangeLang(m_lang_test);
			m_lang_test = m_lang_test == 0 ? 1 : 0;
#endif
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
				_recursion: ChkBoxRecursion.IsChecked != false,
				_keepStruct: ChkBoxKeepStructure.IsChecked != false,
				_compress: ChkBoxCompressAll.IsChecked != false,
				_stayNoMove: ChkBoxStayNoMove.IsChecked == true,
				_pageSizeType: m_pageSizeType,
				_pagesizex: m_useSizeOfFirstPic ? 0 : int.Parse(TextWidth.Text),
				_pagesizey: m_useSizeOfFirstPic ? 0 : int.Parse(TextHeight.Text),
				_parallelOnFileLevel: true,
				_type: m_type,
				_quality: m_quality,
				_archive: m_archive
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
		}

		/// <summary>
		/// 更改语言。
		/// Change Language.
		/// </summary>
		/// <param name="index">default: English, 1: Chinese(S)</param>
		private static void ChangeLang(int index) {
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

		private void Button_Click(object sender, RoutedEventArgs e) {
			WindowMorePreferences dialog = new(m_type, m_quality, m_archive);
			dialog.ShowDialog();

			switch (dialog.CompressType) {
			case "JPEG":
				m_type = 1;
				break;
			case "PNG":
				m_type = 2;
				break;
			default:
				MessageBox.Show(this, $"Failed to set compression type ({dialog.CompressType}). Default is used (JPEG).", Title);
				m_type = 1;
				break;
			}
			m_quality = dialog.CompressQuility;
			m_archive = dialog.ConvertArchive;

			if (m_quality > 100)
				m_quality = 100;
			else if (m_quality < 0)
				m_quality = 0;
		}
	}
}