using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace Timer
{
    public partial class MiniTimerWindow
    {
        private readonly MainWindow parentWindow;
        private readonly string timerType; // "stopwatch" or "countdown"

        public MiniTimerWindow(MainWindow parent, string type)
        {
            InitializeComponent();
            parentWindow = parent;
            timerType = type;

            // 设置标题和按钮文本
            if (type == "stopwatch")
            {
                TitleText.Text = "计时器";
                Title = "计时器";
            }
            else
            {
                TitleText.Text = "倒计时器";
                Title = "倒计时器";
            }

            // 设置按钮文本
            ReturnButton.Content = "返回";
            ExitButton.Content = "退出";

            // 更新控制按钮状态
            UpdateControlButtons();

            // 初始位置：显示在屏幕右上角
            Left = SystemParameters.PrimaryScreenWidth - Width - 50;
            Top = 50;
        }

        public void UpdateTime(string timeText, bool isWarning = false)
        {
            TimeDisplay.Text = timeText;

            // 倒计时警告颜色
            if (isWarning && timerType == "countdown")
            {
                TimeDisplay.Foreground = new SolidColorBrush(Colors.Red);
            }
            else
            {
                TimeDisplay.Foreground = new SolidColorBrush(Colors.White);
            }

            // 更新控制按钮状态
            UpdateControlButtons();
        }

        private void UpdateControlButtons()
        {
            if (timerType == "stopwatch")
            {
                // 获取计时器状态
                var isRunning = parentWindow.IsStopwatchRunning();
                var isPaused = parentWindow.IsStopwatchPaused();

                switch (isRunning)
                {
                    case false when !isPaused:
                        // 播放图标
                        ToggleIcon.Data = Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
                        break;
                    case true when !isPaused:
                        // 暂停图标
                        ToggleIcon.Data = Geometry.Parse("M6,4H10V20H6V4M14,4H18V20H14V4Z");
                        break;
                    case false when true:
                        // 播放图标（继续）
                        ToggleIcon.Data = Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
                        break;
                }
            }
            else // countdown
            {
                // 获取倒计时状态
                var isRunning = parentWindow.IsCountdownRunning();
                var isPaused = parentWindow.IsCountdownPaused();

                switch (isRunning)
                {
                    case false when !isPaused:
                        // 播放图标
                        ToggleIcon.Data = Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
                        break;
                    case true when !isPaused:
                        // 暂停图标
                        ToggleIcon.Data = Geometry.Parse("M6,4H10V20H6V4M14,4H18V20H14V4Z");
                        break;
                    case false when true:
                        // 播放图标（继续）
                        ToggleIcon.Data = Geometry.Parse("M8,5.14V19.14L19,12.14L8,5.14Z");
                        break;
                }
            }
        }

        private void ToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (timerType == "stopwatch")
            {
                parentWindow.ToggleStopwatch();
            }
            else
            {
                parentWindow.ToggleCountdown();
            }

            UpdateControlButtons();
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (timerType == "stopwatch")
            {
                parentWindow.StopStopwatch();
            }
            else
            {
                parentWindow.StopCountdown();
            }

            UpdateControlButtons();
        }

        private void ReturnButton_Click(object sender, RoutedEventArgs e)
        {
            // 显示主窗口
            parentWindow.Show();
            parentWindow.WindowState = WindowState.Normal;
            parentWindow.Activate();

            // 关闭迷你窗口
            Close();
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            // 检查是否有正在运行的计时器
            if (parentWindow.HasRunningTimers())
            {
                var result = MessageBox.Show(
                    "当前有计时器正在运行，退出将自动保存当前会话到历史记录。\n确定要退出吗？",
                    "确认退出",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }
            else
            {
                var result = MessageBox.Show(
                    "确定要退出迷你窗口吗？",
                    "确认退出",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.No)
                {
                    return;
                }
            }

            // 保存当前会话并退出程序
            parentWindow.SaveCurrentSessionAndExit();
        }

        private void DragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // 支持拖动窗口
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            // 确保主窗口重新显示
            if (parentWindow.WindowState == WindowState.Minimized)
            {
                parentWindow.Show();
                parentWindow.WindowState = WindowState.Normal;
            }

            base.OnClosed(e);
        }

        // 双击窗口也可拖动
        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);

            // 支持通过窗口任意位置拖动
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
    }
}