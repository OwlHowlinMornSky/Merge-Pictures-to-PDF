using System.Windows;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// WindowMorePreferences.xaml 的交互逻辑
	/// </summary>
	public partial class WindowMorePreferences : Window {
		protected bool Started {
			get; private set;
		} = false;
		public WindowMorePreferences() {
			InitializeComponent();
#if !DEBUG
			gpBoxDebug.Visibility = Visibility.Hidden;
#endif

			if (Settings1.Default.CompressResizeWidthValue == 0)
				Settings1.Default.CompressResizeWidthValue = (int)(Settings1.Default.PageSizeWidth * 4);
			if (Settings1.Default.CompressResizeHeightValue == 0)
				Settings1.Default.CompressResizeHeightValue = (int)(Settings1.Default.PageSizeHeight * 4);
			if (Settings1.Default.CompressResizeShortValue == 0)
				Settings1.Default.CompressResizeShortValue = (int)(Settings1.Default.PageSizeWidth * 4);
			if (Settings1.Default.CompressResizeLongValue == 0)
				Settings1.Default.CompressResizeLongValue = (int)(Settings1.Default.PageSizeHeight * 4);

			Started = true;
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
			e.Handled = true;
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			Settings1.Default.Save();
		}

		private void ButtonResetSettings_Click(object sender, RoutedEventArgs e) {
			Settings1.Default.Reset();
			if (DataContext is MorePreference) {
				DataContext = new MorePreference();
			}
		}

		private void ButtonLanguage_Click(object sender, RoutedEventArgs e) {
			Settings1.Default.Language = Settings1.Default.Language == 0 ? 1 : 0;
			MainWindow.ChangeLang(Settings1.Default.Language);
		}

		private void ButtonTest_Click(object sender, RoutedEventArgs e) {
			if (DataContext is MorePreference bd) {
				bd.IoMoveProcessed = !bd.IoMoveProcessed;
				bd.IoPdfInFolder = !bd.IoPdfInFolder;
				bd.Quality++;
				bd.Resize = !bd.Resize;
				bd.ResizeWidth = !bd.ResizeWidth;
				bd.ResizeHeight = !bd.ResizeHeight;
				bd.ResizeShort = !bd.ResizeShort;
				bd.ResizeLong = !bd.ResizeLong;
				bd.ResizeReduceByPow2 = !bd.ResizeReduceByPow2;
				bd.ResizeWidthValue = (int.Parse(bd.ResizeWidthValue) + 1).ToString();
				bd.ResizeHeightValue = (int.Parse(bd.ResizeHeightValue) + 1).ToString();
				bd.ResizeShortValue = (int.Parse(bd.ResizeShortValue) + 1).ToString();
				bd.ResizeLongValue = (int.Parse(bd.ResizeLongValue) + 1).ToString();
			}
		}
	}
}
