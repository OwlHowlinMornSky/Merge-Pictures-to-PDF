using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace WpfGui {
	/// <summary>
	/// WindowMorePreferences.xaml 的交互逻辑
	/// </summary>
	public partial class WindowMorePreferences : Window {
		private bool m_reseted = false;

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
		}

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			this.Focus();
			if (Validation.GetHasError(intboxWidth) ||
				Validation.GetHasError(intboxHeight) ||
				Validation.GetHasError(intboxShort) ||
				Validation.GetHasError(intboxLong)) {
				MessageBox.Show(this, App.Current.TryFindResource("InvalidParams").ToString() ?? "Check value please.", this.Title, MessageBoxButton.OK, MessageBoxImage.Error);
				e.Cancel = true;
				return;
			}
			Settings1.Default.Save();
			if(m_reseted) {
				DialogResult = true;
			}
		}

		private void ButtonResetSettings_Click(object sender, RoutedEventArgs e) {
			Settings1.Default.Reset();
			if (DataContext is MorePreference) {
				DataContext = new MorePreference();
			}
			m_reseted = true;
		}

		private void ButtonLanguage_Click(object sender, RoutedEventArgs e) {
			App.LangMngr.CurrentLangId++;
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
				bd.ResizeWidthValue++;
				bd.ResizeHeightValue++;
				bd.ResizeShortValue++;
				bd.ResizeLongValue++;
			}
		}
	}
}
