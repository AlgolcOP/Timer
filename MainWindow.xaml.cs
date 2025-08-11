using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using System.Xml.Serialization;

namespace Timer
{
    /// <summary>
    ///     MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow
    {
        private readonly string historyFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "history.xml");
        private DateTime countdownEndTime; // 用于精确计算剩余时间
        private TimeSpan countdownOriginal;
        private TimeSpan countdownRemaining;
        private DateTime countdownStartTime;

        // 倒计时相关
        private DispatcherTimer countdownTimer;
        private bool isCountdownPaused;
        private bool isCountdownRunning;
        private bool isStopwatchPaused;
        private bool isStopwatchRunning;

        // 迷你窗口
        private MiniTimerWindow miniWindow;
        private TimeSpan stopwatchElapsed;

        private DateTime stopwatchStartTime;

        // 计时器相关
        private DispatcherTimer stopwatchTimer;

        // 历史记录
        private List<TimerRecord> timerHistory;

        public MainWindow()
        {
            InitializeComponent();
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            InitializeTimers();
            LoadHistory(); // 加载历史记录
            UpdateCountdownInputVisibility();
        }

        private void InitializeTimers()
        {
            // 初始化计时器 - 使用更高精度
            stopwatchTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50) // 提高到50ms更新频率
            };
            stopwatchTimer.Tick += StopwatchTimer_Tick;
            stopwatchElapsed = TimeSpan.Zero;

            // 初始化倒计时器 - 使用50ms间隔以获得更平滑的显示
            countdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(50)
            };
            countdownTimer.Tick += CountdownTimer_Tick;
            countdownRemaining = TimeSpan.FromSeconds(30); // 默认30秒
            countdownOriginal = countdownRemaining;

            UpdateStopwatchDisplay();
            UpdateCountdownDisplay();
        }

        // 防止用户意外关闭程序时丢失正在运行的计时
        protected override void OnClosing(CancelEventArgs e)
        {
            if (isStopwatchRunning || isCountdownRunning)
            {
                var result = MessageBox.Show(
                    "当前有计时器正在运行，确定要退出吗？\n正在运行的计时将自动保存到历史记录。",
                    "确认退出",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // 保存正在运行的计时到历史记录
                if (isStopwatchRunning || isStopwatchPaused)
                {
                    AddToHistory(
                        "计时",
                        stopwatchStartTime,
                        DateTime.Now,
                        stopwatchElapsed,
                        "程序退出时自动保存"
                    );
                }

                if (isCountdownRunning || isCountdownPaused)
                {
                    AddToHistory(
                        "倒计时",
                        countdownStartTime,
                        DateTime.Now,
                        countdownOriginal.Subtract(countdownRemaining),
                        "程序退出时自动保存",
                        countdownOriginal
                    );
                }
            }

            SaveHistory();

            miniWindow?.Close();
            miniWindow = null;

            stopwatchTimer?.Stop();
            countdownTimer?.Stop();

            base.OnClosing(e);
        }

#region 计时器功能

        private void ToggleStopwatch_Click(object sender, RoutedEventArgs e)
        {
            switch (isStopwatchRunning)
            {
                case false when !isStopwatchPaused:
                    // 开始计时
                    stopwatchStartTime = DateTime.Now;
                    stopwatchElapsed = TimeSpan.Zero;
                    stopwatchTimer.Start();
                    isStopwatchRunning = true;
                    isStopwatchPaused = false;
                    StartStopwatchBtn.Content = "暂停";
                    break;
                case true when !isStopwatchPaused:
                    // 暂停计时
                    stopwatchTimer.Stop();
                    isStopwatchRunning = false;
                    isStopwatchPaused = true;
                    StartStopwatchBtn.Content = "继续";
                    break;
                case false when isStopwatchPaused:
                    // 继续计时
                    stopwatchStartTime = DateTime.Now.Subtract(stopwatchElapsed);
                    stopwatchTimer.Start();
                    isStopwatchRunning = true;
                    isStopwatchPaused = false;
                    StartStopwatchBtn.Content = "暂停";
                    break;
            }
        }

        private void StopStopwatch_Click(object sender, RoutedEventArgs e)
        {
            if (!isStopwatchRunning && !isStopwatchPaused)
            {
                return;
            }

            stopwatchTimer.Stop();

            // 添加到历史记录
            var endTime = DateTime.Now;
            var duration = stopwatchElapsed;
            AddToHistory("计时", stopwatchStartTime, endTime, duration);

            // 重置状态
            isStopwatchRunning = false;
            isStopwatchPaused = false;
            stopwatchElapsed = TimeSpan.Zero;
            UpdateStopwatchDisplay();
            StartStopwatchBtn.Content = "开始";
        }

        private void StopwatchTimer_Tick(object sender, EventArgs e)
        {
            // 使用实际时间差计算，避免累积误差
            var now = DateTime.Now;
            stopwatchElapsed = now.Subtract(stopwatchStartTime);
            UpdateStopwatchDisplay();
        }

        private void UpdateStopwatchDisplay()
        {
            var format = GetSelectedFormat(StopwatchFormatCombo);
            var timeText = FormatTimeSpan(stopwatchElapsed, format);
            StopwatchDisplay.Text = timeText;

            // 如果迷你窗口打开且是计时器模式，更新迷你窗口
            if (miniWindow != null && miniWindow.IsVisible)
            {
                miniWindow.UpdateTime(timeText);
            }
        }

        private void StopwatchFormat_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (StopwatchDisplay != null)
            {
                UpdateStopwatchDisplay();
            }
        }

        #endregion

        #region 倒计时功能

        private void CountdownInput_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.SelectAll();
            }
        }

        private void SetCountdown_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var mode = GetSelectedFormat(CountdownModeCombo);
                int hours = 0, minutes = 0, seconds = 0;
                TimeSpan newTime;

                switch (mode)
                {
                    case "hh:mm:ss":
                        if (!int.TryParse(HoursInput.Text, out hours) ||
                            !int.TryParse(MinutesInput.Text, out minutes) ||
                            !int.TryParse(SecondsInput.Text, out seconds))
                        {
                            throw new FormatException();
                        }

                        newTime = new TimeSpan(hours, minutes, seconds);
                        break;
                    case "mm:ss":
                        if (!int.TryParse(MinutesInput.Text, out minutes) ||
                            !int.TryParse(SecondsInput.Text, out seconds))
                        {
                            throw new FormatException();
                        }

                        newTime = new TimeSpan(0, minutes, seconds);
                        break;
                    case "ss":
                        if (!int.TryParse(SecondsInput.Text, out seconds))
                        {
                            throw new FormatException();
                        }

                        newTime = new TimeSpan(0, 0, seconds);
                        break;
                    default:
                        newTime = TimeSpan.FromSeconds(30);
                        break;
                }

                if (hours < 0 || hours > 60 || minutes < 0 || minutes > 60 || seconds < 0 || seconds > 60)
                {
                    throw new FormatException();
                }
                if (newTime.TotalSeconds > 0)
                {
                    countdownRemaining = newTime;
                    countdownOriginal = newTime;
                    UpdateCountdownDisplay();
                    MessageBox.Show($"倒计时已设置为 {FormatTimeSpan(newTime, "hh:mm:ss")}", "设置成功",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("请设置有效的时间（大于0）", "设置错误",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (FormatException)
            {
                MessageBox.Show("请输入有效的数字", "输入错误",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"发生错误: {ex.Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ToggleCountdown_Click(object sender, RoutedEventArgs e)
        {
            switch (isCountdownRunning)
            {
                case false when !isCountdownPaused:
                {
                    // 开始倒计时
                    if (countdownRemaining.TotalSeconds > 0)
                    {
                        countdownStartTime = DateTime.Now;
                        countdownEndTime = countdownStartTime.Add(countdownRemaining);
                        countdownTimer.Start();
                        isCountdownRunning = true;
                        isCountdownPaused = false;
                        StartCountdownBtn.Content = "暂停";
                    }

                    break;
                }
                case true when !isCountdownPaused:
                {
                    // 暂停倒计时
                    countdownTimer.Stop();
                    // 计算剩余时间
                    var now = DateTime.Now;
                    countdownRemaining = countdownEndTime.Subtract(now);
                    if (countdownRemaining.TotalSeconds < 0)
                    {
                        countdownRemaining = TimeSpan.Zero;
                    }

                    isCountdownRunning = false;
                    isCountdownPaused = true;
                    StartCountdownBtn.Content = "继续";
                    UpdateCountdownDisplay();
                    break;
                }
                case false when isCountdownPaused:
                {
                    // 继续倒计时
                    if (countdownRemaining.TotalSeconds > 0)
                    {
                        countdownStartTime = DateTime.Now;
                        countdownEndTime = countdownStartTime.Add(countdownRemaining);
                        countdownTimer.Start();
                        isCountdownRunning = true;
                        isCountdownPaused = false;
                        StartCountdownBtn.Content = "暂停";
                    }

                    break;
                }
            }
        }

        private void StopCountdown_Click(object sender, RoutedEventArgs e)
        {
            if (!isCountdownRunning && !isCountdownPaused)
            {
                return;
            }

            countdownTimer.Stop();

            // 添加到历史记录
            var endTime = DateTime.Now;
            var actualDuration = countdownOriginal.Subtract(countdownRemaining);
            AddToHistory("倒计时", countdownStartTime, endTime, actualDuration, "", countdownOriginal);

            // 重置状态
            isCountdownRunning = false;
            isCountdownPaused = false;
            countdownRemaining = countdownOriginal;
            UpdateCountdownDisplay();
            StartCountdownBtn.Content = "开始";
        }

        private void CountdownTimer_Tick(object sender, EventArgs e)
        {
            var now = DateTime.Now;
            countdownRemaining = countdownEndTime.Subtract(now);

            if (countdownRemaining.TotalSeconds <= 0)
            {
                countdownRemaining = TimeSpan.Zero;
                countdownTimer.Stop();

                // 倒计时结束
                var endTime = DateTime.Now;
                AddToHistory("倒计时", countdownStartTime, endTime, countdownOriginal, "", countdownOriginal);

                isCountdownRunning = false;
                isCountdownPaused = false;
                StartCountdownBtn.Content = "开始";

                MessageBox.Show("倒计时结束！", "提醒", MessageBoxButton.OK, MessageBoxImage.Information);

                // 重置为原始时间
                countdownRemaining = countdownOriginal;
            }

            UpdateCountdownDisplay();
        }

        private void UpdateCountdownDisplay()
        {
            var format = GetSelectedFormat(CountdownFormatCombo);
            var timeText = FormatTimeSpan(countdownRemaining, format);
            CountdownDisplay.Text = timeText;

            // 当时间不足10秒时，显示红色警告
            var isWarning = countdownRemaining.TotalSeconds <= 10 && countdownRemaining.TotalSeconds > 0;
            CountdownDisplay.Foreground =
                isWarning ? new SolidColorBrush(Colors.Red) : new SolidColorBrush(Colors.White);

            // 如果迷你窗口打开且是倒计时模式，更新迷你窗口
            if (miniWindow != null && miniWindow.IsVisible)
            {
                miniWindow.UpdateTime(timeText, isWarning);
            }
        }

        private void CountdownFormat_Changed(object sender, SelectionChangedEventArgs e)
        {
            if (CountdownDisplay != null)
            {
                UpdateCountdownDisplay();
            }
        }

        private void CountdownMode_Changed(object sender, SelectionChangedEventArgs e)
        {
            UpdateCountdownInputVisibility();
        }

        private void UpdateCountdownInputVisibility()
        {
            if (CountdownModeCombo?.SelectedItem == null || CountdownInputPanel == null)
            {
                return;
            }

            var mode = GetSelectedFormat(CountdownModeCombo);
            var children = CountdownInputPanel.Children.OfType<UIElement>().ToList();

            switch (mode)
            {
                case "hh:mm:ss":
                    // 显示所有输入框
                    foreach (var child in children)
                    {
                        child.Visibility = Visibility.Visible;
                    }

                    break;
                case "mm:ss":
                    // 隐藏小时输入
                    if (children.Count >= 2)
                    {
                        children[0].Visibility = Visibility.Collapsed; // HoursInput
                        children[1].Visibility = Visibility.Collapsed; // 第一个冒号
                    }

                    break;
                case "ss":
                    // 只显示秒输入
                    for (var i = 0; i < children.Count - 1; i++)
                    {
                        children[i].Visibility = Visibility.Collapsed;
                    }

                    break;
            }
        }

#endregion

#region 迷你窗口功能

        private void MiniStopwatch_Click(object sender, RoutedEventArgs e)
        {
            ShowMiniWindow("stopwatch");
        }

        private void MiniCountdown_Click(object sender, RoutedEventArgs e)
        {
            ShowMiniWindow("countdown");
        }

        private void ShowMiniWindow(string timerType)
        {
            // 如果已经有迷你窗口，先关闭
            if (miniWindow != null)
            {
                miniWindow.Close();
                miniWindow = null;
            }

            // 创建新的迷你窗口
            miniWindow = new MiniTimerWindow(this, timerType);

            // 更新当前时间显示
            if (timerType == "stopwatch")
            {
                var format = GetSelectedFormat(StopwatchFormatCombo);
                var timeText = FormatTimeSpan(stopwatchElapsed, format);
                miniWindow.UpdateTime(timeText);
            }
            else
            {
                var format = GetSelectedFormat(CountdownFormatCombo);
                var timeText = FormatTimeSpan(countdownRemaining, format);
                var isWarning = countdownRemaining.TotalSeconds <= 10 && countdownRemaining.TotalSeconds > 0;
                miniWindow.UpdateTime(timeText, isWarning);
            }

            // 显示迷你窗口
            miniWindow.Show();

            // 隐藏主窗口
            Hide();
        }

#endregion

#region 历史记录功能

        private void LoadHistory()
        {
            try
            {
                if (File.Exists(historyFilePath))
                {
                    using (var stream = new FileStream(historyFilePath, FileMode.Open))
                    {
                        var serializer = new XmlSerializer(typeof(List<TimerRecord>));
                        timerHistory = (List<TimerRecord>)serializer.Deserialize(stream);
                    }
                }
                else
                {
                    timerHistory = new List<TimerRecord>();
                }
            }
            catch (Exception ex)
            {
                // 如果加载失败，创建新的历史记录列表
                timerHistory = new List<TimerRecord>();
                MessageBox.Show($"加载历史记录失败: {ex.Message}", "警告", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            UpdateHistoryDisplay();
        }

        private void SaveHistory()
        {
            try
            {
                // 确保目录存在
                var directory = Path.GetDirectoryName(historyFilePath);
                if (directory is null)
                {
                    throw new InvalidOperationException("历史记录文件路径无效");
                }

                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                using (var stream = new FileStream(historyFilePath, FileMode.Create))
                {
                    var serializer = new XmlSerializer(typeof(List<TimerRecord>));
                    serializer.Serialize(stream, timerHistory);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"保存历史记录失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void AddToHistory(string type, DateTime startTime, DateTime endTime, TimeSpan duration,
            string name = "", TimeSpan originalTime = default)
        {
            // 如果name为空，则自动生成带计数的名称
            if (string.IsNullOrEmpty(name))
            {
                name = GenerateAutoName(type);
            }

            var record = new TimerRecord(type, startTime, endTime, duration, name, originalTime);
            timerHistory.Insert(0, record); // 插入到开头，最新的在上面

            // 限制历史记录数量，保留最近1000条
            if (timerHistory.Count > 1000)
            {
                timerHistory = timerHistory.Take(1000).ToList();
            }

            UpdateHistoryDisplay();
            SaveHistory(); // 每次添加记录后自动保存
        }

        private string GenerateAutoName(string type)
        {
            // 计算相同类型的未重命名记录数量
            var count = timerHistory.Count(record =>
                record.Type == type &&
                (string.IsNullOrEmpty(record.Name) ||
                 (record.Name.StartsWith(type) &&
                  record.Name.Length > type.Length &&
                  char.IsDigit(record.Name[type.Length]))));

            return $"{type}{count + 1}";
        }

        private void UpdateHistoryDisplay()
        {
            HistoryPanel.Children.Clear();

            foreach (var record in timerHistory.Take(50)) // 只显示最近50条记录以优化性能
            {
                var panel = CreateHistoryItem(record);
                HistoryPanel.Children.Add(panel);
            }
        }

        private Border CreateHistoryItem(TimerRecord record)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x35, 0x3C, 0x48)),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(0, 0, 0, 8),
                Padding = new Thickness(10),
                Cursor = Cursors.Hand,
                Tag = record // 存储记录引用以便后续操作
            };

            // 使用Grid布局来放置内容和删除按钮
            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // 主内容区域
            var contentStackPanel = new StackPanel();
            Grid.SetColumn(contentStackPanel, 0);

            // 显示自定义名称（如果有）
            if (!string.IsNullOrEmpty(record.Name))
            {
                var nameText = new TextBlock
                {
                    Text = $"📌 {record.Name}",
                    FontWeight = FontWeights.Bold,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Colors.LightGreen),
                    Margin = new Thickness(0, 0, 20, 3) // 右边距为删除按钮留空间
                };
                contentStackPanel.Children.Add(nameText);
            }

            // 标题行 - 与删除按钮水平居中对齐
            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var typeText = new TextBlock
            {
                Text = record.Type,
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                Foreground = new SolidColorBrush(Colors.White),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 20, 0) // 右边距为删除按钮留空间
            };
            Grid.SetColumn(typeText, 0);
            titleGrid.Children.Add(typeText);
            contentStackPanel.Children.Add(titleGrid);

            // 根据类型创建不同的布局
            if (record.Type == "倒计时")
            {
                // 倒计时记录：两列布局
                var timeInfoGrid = new Grid();
                timeInfoGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                timeInfoGrid.ColumnDefinitions.Add(
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                timeInfoGrid.Margin = new Thickness(0, 5, 20, 0); // 右边距为删除按钮留空间

                // 第一列：开始时间和结束时间
                var timeColumn = new StackPanel();
                Grid.SetColumn(timeColumn, 0);

                var startTimeText = new TextBlock
                {
                    Text = $"开始时间: {record.StartTime:HH:mm:ss}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    Margin = new Thickness(0, 0, 0, 2)
                };
                timeColumn.Children.Add(startTimeText);

                var endTimeText = new TextBlock
                {
                    Text = $"结束时间: {record.EndTime:HH:mm:ss}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    Margin = new Thickness(0, 0, 0, 2)
                };
                timeColumn.Children.Add(endTimeText);

                timeInfoGrid.Children.Add(timeColumn);

                // 第二列：设置时间和持续时间
                var durationColumn = new StackPanel();
                Grid.SetColumn(durationColumn, 1);

                var originalTimeText = new TextBlock
                {
                    Text = $"设置时间: {FormatTimeSpan(record.OriginalTime, "hh:mm:ss")}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightBlue),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 2)
                };
                durationColumn.Children.Add(originalTimeText);

                var durationText = new TextBlock
                {
                    Text = $"持续时间: {FormatTimeSpan(record.Duration, "hh:mm:ss")}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightBlue),
                    FontWeight = FontWeights.SemiBold
                };
                durationColumn.Children.Add(durationText);

                timeInfoGrid.Children.Add(durationColumn);
                contentStackPanel.Children.Add(timeInfoGrid);
            }
            else
            {
                // 计时记录：保持原有布局
                var startTimeText = new TextBlock
                {
                    Text = $"开始时间: {record.StartTime:HH:mm:ss}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    Margin = new Thickness(0, 5, 20, 2) // 右边距为删除按钮留空间
                };
                contentStackPanel.Children.Add(startTimeText);

                var endTimeText = new TextBlock
                {
                    Text = $"结束时间: {record.EndTime:HH:mm:ss}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightGray),
                    Margin = new Thickness(0, 0, 20, 2) // 右边距为删除按钮留空间
                };
                contentStackPanel.Children.Add(endTimeText);

                var durationText = new TextBlock
                {
                    Text = $"持续时间: {FormatTimeSpan(record.Duration, "hh:mm:ss")}",
                    FontSize = 10,
                    Foreground = new SolidColorBrush(Colors.LightBlue),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 20, 0) // 右边距为删除按钮留空间
                };
                contentStackPanel.Children.Add(durationText);
            }

            // 添加点击提示
            var clickHintText = new TextBlock
            {
                Text = "💡 点击可为此记录添加/编辑备注名称",
                FontSize = 9,
                Foreground = new SolidColorBrush(Colors.Gray),
                Margin = new Thickness(0, 3, 20, 0), // 右边距为删除按钮留空间
                FontStyle = FontStyles.Italic
            };
            contentStackPanel.Children.Add(clickHintText);

            mainGrid.Children.Add(contentStackPanel);

            // 创建删除按钮 - 更小尺寸，与标题水平居中对齐
            var deleteButton = new Button
            {
                Width = 16,
                Height = 16,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(5, 0, 0, 0), // 与标题行对齐
                Cursor = Cursors.Hand,
                ToolTip = "删除此记录",
                Tag = record
            };
            Grid.SetColumn(deleteButton, 1);

            // 删除按钮的内容 - 更小的X图标
            var deletePath = new System.Windows.Shapes.Path
            {
                Fill = new SolidColorBrush(Colors.White),
                Width = 8,
                Height = 8,
                Stretch = Stretch.Uniform,
                Data = Geometry.Parse(
                    "M19,6.41L17.59,5L12,10.59L6.41,5L5,6.41L10.59,12L5,17.59L6.41,19L12,13.41L17.59,19L19,17.59L13.41,12L19,6.41Z"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            deleteButton.Content = deletePath;

            // 创建删除按钮的样式
            var deleteButtonStyle = new Style(typeof(Button));
            deleteButtonStyle.Setters.Add(new Setter(BackgroundProperty, new SolidColorBrush(Colors.Transparent)));
            deleteButtonStyle.Setters.Add(new Setter(BorderThicknessProperty, new Thickness(0)));

            var deleteButtonTemplate = new ControlTemplate(typeof(Button));
            var borderFactory = new FrameworkElementFactory(typeof(Border));
            borderFactory.SetValue(Border.BackgroundProperty, new TemplateBindingExtension(BackgroundProperty));
            borderFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
            borderFactory.SetValue(Border.BorderThicknessProperty, new Thickness(0));

            var contentPresenterFactory = new FrameworkElementFactory(typeof(ContentPresenter));
            contentPresenterFactory.SetValue(HorizontalAlignmentProperty, HorizontalAlignment.Center);
            contentPresenterFactory.SetValue(VerticalAlignmentProperty, VerticalAlignment.Center);
            borderFactory.AppendChild(contentPresenterFactory);

            deleteButtonTemplate.VisualTree = borderFactory;

            // 添加鼠标悬停效果
            var hoverTrigger = new Trigger { Property = IsMouseOverProperty, Value = true };
            hoverTrigger.Setters.Add(new Setter(BackgroundProperty,
                new SolidColorBrush(Color.FromArgb(50, 255, 255, 255))));
            deleteButtonTemplate.Triggers.Add(hoverTrigger);

            deleteButtonStyle.Setters.Add(new Setter(TemplateProperty, deleteButtonTemplate));
            deleteButton.Style = deleteButtonStyle;

            // 添加删除按钮点击事件
            deleteButton.Click += DeleteHistoryItem_Click;

            // 修复：改用MouseLeftButtonUp事件，并正确处理事件冒泡
            deleteButton.MouseLeftButtonUp += (s, e) =>
            {
                e.Handled = true; // 阻止事件继续传播到父元素
                DeleteHistoryItem_Click(s, new RoutedEventArgs());
            };

            mainGrid.Children.Add(deleteButton);

            // 为主内容区域添加点击事件（只在内容区域点击时触发）
            contentStackPanel.MouseLeftButtonUp += (s, e) =>
            {
                // 确保点击的不是删除按钮区域
                if (!IsPointInDeleteButtonArea(e.GetPosition(mainGrid), deleteButton))
                {
                    HistoryItem_Click(border,
                        new MouseButtonEventArgs(Mouse.PrimaryDevice, 0, MouseButton.Left)
                            { RoutedEvent = MouseLeftButtonUpEvent });
                }
            };

            border.Child = mainGrid;
            return border;
        }

        // 辅助方法：检查点击位置是否在删除按钮区域
        private bool IsPointInDeleteButtonArea(Point clickPoint, Button deleteButton)
        {
            var buttonBounds = new Rect(
                deleteButton.Margin.Left + ((Grid)deleteButton.Parent).ColumnDefinitions[0].ActualWidth,
                deleteButton.Margin.Top,
                deleteButton.ActualWidth + deleteButton.Margin.Right,
                deleteButton.ActualHeight + deleteButton.Margin.Bottom
            );

            return buttonBounds.Contains(clickPoint);
        }

        private void DeleteHistoryItem_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TimerRecord record = null;

                // 尝试从sender获取record
                if (sender is Button button && button.Tag is TimerRecord buttonRecord)
                {
                    record = buttonRecord;
                }
                // 如果sender不是Button，可能是从MouseLeftButtonUp调用的
                else if (sender is Button btn)
                {
                    record = btn.Tag as TimerRecord;
                }

                if (record == null)
                {
                    MessageBox.Show("无法找到要删除的记录", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                var result = MessageBox.Show(
                    $"确定要删除这条记录吗？\n\n{record.Type}: {record.Name}\n开始时间: {record.StartTime:yyyy-MM-dd HH:mm:ss}\n持续时间: {FormatTimeSpan(record.Duration, "hh:mm:ss")}",
                    "确认删除",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question,
                    MessageBoxResult.No);

                if (result == MessageBoxResult.Yes)
                {
                    // 从历史记录列表中移除
                    var removed = timerHistory.Remove(record);

                    if (removed)
                    {
                        // 更新显示
                        UpdateHistoryDisplay();

                        // 保存更改
                        SaveHistory();
                    }
                    else
                    {
                        MessageBox.Show("删除失败：记录未找到", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"删除记录时发生错误：{ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void HistoryItem_Click(object sender, MouseButtonEventArgs _)
        {
            if (!(sender is Border border) || !(border.Tag is TimerRecord record))
            {
                return;
            }

            // 创建输入对话框
            var inputDialog = new Window
            {
                Title = "为计时记录添加备注",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Background = new SolidColorBrush(Color.FromRgb(0x46, 0x4C, 0x5B)),
                FontFamily = new FontFamily("Microsoft YaHei"),
                Foreground = new SolidColorBrush(Colors.White)
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleText = new TextBlock
            {
                Text = $"为 {record.Type} 记录添加备注名称:",
                FontSize = 14,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(20, 20, 20, 10),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            Grid.SetRow(titleText, 0);
            grid.Children.Add(titleText);

            var textBox = new TextBox
            {
                Text = record.Name,
                FontSize = 14,
                Margin = new Thickness(20, 10, 20, 20),
                Padding = new Thickness(4, 2, 4, 2), // 优化文本显示区域
                Background = new SolidColorBrush(Color.FromRgb(0x41, 0x5A, 0x71)),
                Foreground = new SolidColorBrush(Colors.White),
                BorderBrush = new SolidColorBrush(Colors.Transparent),
                VerticalContentAlignment = VerticalAlignment.Center,
                CaretBrush = new SolidColorBrush(Colors.White) // 修复光标不可见问题
            };
            Grid.SetRow(textBox, 1);
            grid.Children.Add(textBox);

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 0, 0, 20)
            };

            var okButton = new Button
            {
                Content = "确定",
                Width = 80,
                Height = 35,
                Margin = new Thickness(0, 0, 10, 0),
                Background = new SolidColorBrush(Color.FromRgb(0x41, 0x5A, 0x71)),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Microsoft YaHei")
            };

            var cancelButton = new Button
            {
                Content = "取消",
                Width = 80,
                Height = 35,
                Background = new SolidColorBrush(Color.FromRgb(0x41, 0x5A, 0x71)),
                Foreground = new SolidColorBrush(Colors.White),
                FontFamily = new FontFamily("Microsoft YaHei")
            };

            okButton.Click += (s, args) =>
            {
                record.Name = textBox.Text.Trim();
                UpdateHistoryDisplay();
                SaveHistory();
                inputDialog.Close();
            };

            cancelButton.Click += (s, args) => inputDialog.Close();

            // 支持回车键确定
            textBox.KeyDown += (s, args) =>
            {
                if (args.Key == Key.Enter)
                {
                    okButton.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                }
            };

            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            Grid.SetRow(buttonPanel, 2);
            grid.Children.Add(buttonPanel);

            inputDialog.Content = grid;
            textBox.Focus();
            textBox.SelectAll();
            inputDialog.ShowDialog();
        }

        private void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("确定要清空所有历史记录吗？", "确认清空",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes)
            {
                return;
            }

            timerHistory.Clear();
            UpdateHistoryDisplay();
            SaveHistory(); // 保存清空操作
        }

#endregion

#region 公共方法供迷你窗口使用

        public void SaveCurrentSessionAndExit()
        {
            // 如果计时器正在运行，保存到历史记录
            if (isStopwatchRunning || isStopwatchPaused)
            {
                stopwatchTimer?.Stop();
                var endTime = DateTime.Now;
                var duration = stopwatchElapsed;
                AddToHistory("计时", stopwatchStartTime, endTime, duration, "迷你模式退出时自动保存");
            }

            // 如果倒计时正在运行，保存到历史记录
            if (isCountdownRunning || isCountdownPaused)
            {
                countdownTimer?.Stop();
                var endTime = DateTime.Now;
                var actualDuration = countdownOriginal.Subtract(countdownRemaining);
                AddToHistory("倒计时", countdownStartTime, endTime, actualDuration, "迷你模式退出时自动保存", countdownOriginal);
            }

            // 保存历史记录
            SaveHistory();

            // 清理资源
            stopwatchTimer?.Stop();
            countdownTimer?.Stop();

            // 关闭迷你窗口
            if (miniWindow != null)
            {
                miniWindow.Close();
                miniWindow = null;
            }

            // 退出程序
            Application.Current.Shutdown();
        }

        public bool HasRunningTimers() => isStopwatchRunning || isCountdownRunning;

        // 迷你窗口控制方法
        public bool IsStopwatchRunning() => isStopwatchRunning;

        public bool IsStopwatchPaused() => isStopwatchPaused;

        public bool IsCountdownRunning() => isCountdownRunning;

        public bool IsCountdownPaused() => isCountdownPaused;

        public void ToggleStopwatch()
        {
            ToggleStopwatch_Click(null, null);
        }

        public void StopStopwatch()
        {
            StopStopwatch_Click(null, null);
        }

        public void ToggleCountdown()
        {
            ToggleCountdown_Click(null, null);
        }

        public void StopCountdown()
        {
            StopCountdown_Click(null, null);
        }

#endregion

#region 辅助方法

        private string GetSelectedFormat(ComboBox comboBox)
        {
            if (comboBox?.SelectedItem is ComboBoxItem item && item.Content != null)
            {
                return item.Content.ToString();
            }

            return "hh:mm:ss";
        }

        private string FormatTimeSpan(TimeSpan timeSpan, string format)
        {
            switch (format)
            {
                case "hh:mm:ss":
                    return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
                case "mm:ss":
                    return $"{(int)timeSpan.TotalMinutes:D2}:{timeSpan.Seconds:D2}";
                case "ss":
                    return $"{(int)timeSpan.TotalSeconds:D2}";
                default:
                    return $"{(int)timeSpan.TotalHours:D2}:{timeSpan.Minutes:D2}:{timeSpan.Seconds:D2}";
            }
        }

#endregion
    }

    // 计时记录数据类
    [Serializable]
    public class TimerRecord
    {
        public TimerRecord()
        {
            // 默认构造函数，用于XML序列化
        }

        public TimerRecord(string type, DateTime startTime, DateTime endTime, TimeSpan duration, string name = "",
            TimeSpan originalTime = default)
        {
            Type = type;
            StartTime = startTime;
            EndTime = endTime;
            Duration = duration;
            Name = name;
            OriginalTime = originalTime;
            Id = Guid.NewGuid();
        }

        public string Type { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public TimeSpan Duration { get; set; }
        public TimeSpan OriginalTime { get; set; } = TimeSpan.Zero; // 倒计时的原始设置时间
        public string Name { get; set; } = ""; // 用户自定义名称
        public Guid Id { get; set; } = Guid.NewGuid(); // 唯一标识符
    }
}