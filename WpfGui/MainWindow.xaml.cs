﻿using System.Windows;
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

		public MainWindow() {
			InitializeComponent();
			RadioBtnFixedWidth.IsChecked = true; // 默认固定宽度。

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
			ChangeLang();
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
			m_processor.Set(
				recursion: ChkBoxRecursion.IsChecked != false,
				keepStruct: ChkBoxKeepStructure.IsChecked != false,
				compress: ChkBoxCompressAll.IsChecked != false,
				stayNoMove: ChkBoxStayNoMove.IsChecked == true,
				pageSizeType: m_pageSizeType,
				pagesizex: m_useSizeOfFirstPic ? 0 : int.Parse(TextWidth.Text),
				pagesizey: m_useSizeOfFirstPic ? 0 : int.Parse(TextHeight.Text)
			);
			if (m_processor.Start(paths) == false) {
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

#if DEBUG
		private int m_lang_test = 1;
		private void ChangeLang() {
			ResourceDictionary rd = [];
			switch (m_lang_test) {
			case 1:
				rd.Source = new Uri("DictionaryMainGUI.zh-CN.xaml", UriKind.Relative);
				m_lang_test = 2;
				break;
			default:
				rd.Source = new Uri("DictionaryMainGUI.xaml", UriKind.Relative);
				m_lang_test = 1;
				break;
			}
			App.Current.Resources.MergedDictionaries.Clear();
			App.Current.Resources.MergedDictionaries.Add(rd);
			return;
		}
#else
		private void ChangeLang() {}
#endif
	}
}