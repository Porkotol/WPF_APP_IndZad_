using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace WPF_APP_IndZad
{
    public partial class MainWindow : Window
    {
        private readonly Random random = new Random();  // випадковi числ для кольорів дисків
        private int moveCount;
        private int minMoveCount;   //2^n - 1
        private DispatcherTimer gameTimer;
        private TimeSpan elapsedTime; //з початку гри
        private bool isTimeMode; //режим
        private bool isTimerRunning;

        public MainWindow()
        {
            InitializeComponent(); 
            diskCountComboBox.ItemsSource = Enumerable.Range(2, 9).Select(i => i.ToString()); // ComboBox  від 2 до 10 дисків
            diskCountComboBox.SelectedIndex = 3;  
            Loaded += (s, e) => InitializeGame();
        }
        private void InitializeGame()
        {
            StopTimer();  // 
            isTimerRunning = false;
            timeCounterText.Visibility = isTimeMode ? Visibility.Visible : Visibility.Collapsed; // відображаємо або ховаємо лічильник часу
            timeModeButton.Content = isTimeMode ? "Звичайний" : "На час";
            diskCountComboBox.IsEnabled = true;  // змiна кiльк диск
            leftRod.Children.Clear(); // clear rect
            middleRod.Children.Clear();
            rightRod.Children.Clear();
            moveCount = 0;
            minMoveCount = (int)Math.Pow(2, GetDiskCount()) - 1;
            UpdateMoveCounter(); 
            solvedMessage.Visibility = Visibility.Collapsed;
            elapsedTime = TimeSpan.Zero;
            UpdateTimeCounter();
            var rodWidth = leftRod.ActualWidth - 20;  //розмiри вiд кiльк
            var diskHeight = (leftRod.ActualHeight - 10) / GetDiskCount();


            for (int i = 0; i < GetDiskCount(); i++) //cтворення диск
            {
                var disk = new Rectangle
                {
                
                    Width = rodWidth - i * (2 * rodWidth / (2 * GetDiskCount())), //кожен менше
                    Height = diskHeight - 2,  // зазор

                    Stroke = Brushes.Black, 
                    StrokeThickness = 1,
                    RadiusX = diskHeight / 4,
                    RadiusY = diskHeight / 4,
                    Tag = i + 1, //диск тег
                    Style = (Style)FindResource("DiskStyle"),  
                    RenderTransform = new TranslateTransform()  //  анімація доп
                };


                var gradient = new LinearGradientBrush  // градієнт для диска
                {
                    StartPoint = new Point(0.5, 0),
                    EndPoint = new Point(0.5, 1)
                };
                var color = Color.FromRgb(
                    (byte)random.Next(100, 255),
                    (byte)random.Next(100, 255),
                    (byte)random.Next(100, 255));

                gradient.GradientStops.Add(new GradientStop(Colors.White, 0));
                gradient.GradientStops.Add(new GradientStop(color, 1));
                disk.Fill = gradient;  // присв заповнення

                disk.MouseDown += Disk_MouseDown;

                DockPanel.SetDock(disk, Dock.Bottom);
                leftRod.Children.Add(disk);
            }
        }

        private int GetDiskCount() => int.Parse(diskCountComboBox.SelectedItem.ToString()); //рядок в числ диск

        private void UpdateMoveCounter() => moveCounterText.Text = $"Ходи: {moveCount} (мінімум: {minMoveCount})"; //оновл

        private void UpdateTimeCounter() //оновл тайм
        {
            timeCounterText.Text = $"Час: {elapsedTime:mm\\:ss}";
        }

        private void CheckSolution() // Перевірка на задачу
        {

            if (rightRod.Children.Count == GetDiskCount()) //всі диски у правому стержні
            {
                StopTimer();  

                if (isTimeMode)
                    timeResultText.Text = $"Час: {elapsedTime:mm\\:ss} | Ходи: {moveCount}";
                else
                    timeResultText.Text = $"| Ходи: {moveCount}";

                solvedMessage.Visibility = Visibility.Visible;

                
                var anim = new DoubleAnimation //пульсацiя перемога
                {
                    From = 1,
                    To = 0.8,  
                    Duration = TimeSpan.FromSeconds(0.5),
                    AutoReverse = true,
                    RepeatBehavior = new RepeatBehavior(3)
                };
                solvedMessage.BeginAnimation(UIElement.OpacityProperty, anim);
            }
        }


        private void Disk_MouseDown(object sender, MouseButtonEventArgs e)// диск натискання
        {
            if (e.ChangedButton != MouseButton.Left) return;  // лiва кнопка
            var disk = (Rectangle)sender;

            if (IsTopDisk(disk))
            {
                var anim = new DoubleAnimation { To = 1.1, Duration = TimeSpan.FromMilliseconds(200) }; 
                disk.BeginAnimation(OpacityProperty, anim);
                DragDrop.DoDragDrop(disk, disk, DragDropEffects.Move);
            }
            else
            {

                var transform = new TranslateTransform(); // Для нижн вiбр помлк 
                disk.RenderTransform = transform;

                var vibrateAnim = new DoubleAnimationUsingKeyFrames();
                vibrateAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(0)));
                vibrateAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-5, TimeSpan.FromMilliseconds(50)));
                vibrateAnim.KeyFrames.Add(new LinearDoubleKeyFrame(5, TimeSpan.FromMilliseconds(100)));
                vibrateAnim.KeyFrames.Add(new LinearDoubleKeyFrame(-5, TimeSpan.FromMilliseconds(150)));
                vibrateAnim.KeyFrames.Add(new LinearDoubleKeyFrame(5, TimeSpan.FromMilliseconds(200)));
                vibrateAnim.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(250)));
                vibrateAnim.Completed += (s, args) => disk.RenderTransform = null;
                transform.BeginAnimation(TranslateTransform.XProperty, vibrateAnim);
            }
        }


        private bool IsTopDisk(Rectangle disk) // перевiрка на верхнiй
        {
            var panel = disk.Parent as DockPanel;
            return panel != null && panel.Children.Count > 0 && panel.Children[panel.Children.Count - 1] == disk;
        }

        private DockPanel GetTargetPanel(object target)
        {
            if (target is DockPanel panel) return panel;
            if (target is Rectangle disk) return disk.Parent as DockPanel;
            return null;
        }

        private async void MoveDisk(Rectangle disk, DockPanel targetPanel)  // анімація переміщення диска 
        {
            var sourcePanel = disk.Parent as DockPanel;
            var sourcePos = sourcePanel.TranslatePoint(new Point(0, 0), this); //поз
            var targetPos = targetPanel.TranslatePoint(new Point(0, 0), this);
            var transformGroup = new TransformGroup();         //вгору 
            var translate = new TranslateTransform();
            transformGroup.Children.Add(translate);
            disk.RenderTransform = transformGroup;
            var liftAnim = new DoubleAnimation(0, -50, TimeSpan.FromMilliseconds(150));
            translate.BeginAnimation(TranslateTransform.YProperty, liftAnim);
            await Task.Delay(150);
            double deltaX = (targetPos.X + targetPanel.ActualWidth / 2) - (sourcePos.X + sourcePanel.ActualWidth / 2); //по X 
            var moveXAnim = new DoubleAnimation(0, deltaX, TimeSpan.FromMilliseconds(300));
            translate.BeginAnimation(TranslateTransform.XProperty, moveXAnim);
            await Task.Delay(300);
            var dropAnim = new DoubleAnimation(-50, 0, TimeSpan.FromMilliseconds(150)); // диск вниз
            translate.BeginAnimation(TranslateTransform.YProperty, dropAnim);
            await Task.Delay(150);
            sourcePanel.Children.Remove(disk);
            targetPanel.Children.Add(disk);
            disk.RenderTransform = null;

            moveCount++; // оновл та перев
            UpdateMoveCounter();
            CheckSolution();
        }

        private void GamePanel_DragEnter(object sender, DragEventArgs e) //перевiрка на вставлення
        {
            var panel = GetTargetPanel(e.Source);
            if (panel == null) return;
            var disk = e.Data.GetData(typeof(Rectangle)) as Rectangle;
            if (disk == null) return;
            var old = disk.Parent as DockPanel;
            if (old.Children.IndexOf(disk) != old.Children.Count - 1)
            {
                e.Effects = DragDropEffects.None;
                e.Handled = true;
                return;
            }
            var count = panel.Children.Count; //не бiльш на менш
            var topWidth = count > 0 ? ((Rectangle)panel.Children[count - 1]).Width : double.MaxValue;
            e.Effects = disk.Width <= topWidth ? DragDropEffects.Move : DragDropEffects.None;
            e.Handled = true;
        }

        private void GamePanel_Drop(object sender, DragEventArgs e) //перемiщ
        {
            var panel = GetTargetPanel(e.Source);
            if (panel == null) return;
            var disk = e.Data.GetData(typeof(Rectangle)) as Rectangle;
            if (disk == null || panel == disk.Parent) return;
            var count = panel.Children.Count;
            var topWidth = count > 0 ? ((Rectangle)panel.Children[count - 1]).Width : double.MaxValue;
            if (disk.Width <= topWidth)
            {
                if (isTimeMode && moveCount == 0 && !isTimerRunning) //запуск якщо на час 
                    StartTimer();

                MoveDisk(disk, panel);
            }
        }

        private void DiskCountComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e) //кiльк диск
        {
            if (IsLoaded) InitializeGame();
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e) => InitializeGame();

        private void TimeModeButton_Click(object sender, RoutedEventArgs e) //перемикання режиму
        {
            isTimeMode = !isTimeMode;
            timeCounterText.Visibility = isTimeMode ? Visibility.Visible : Visibility.Collapsed;
            timeModeButton.Content = isTimeMode ? "Звичайний" : "На час";
            if (isTimeMode && isTimerRunning)  // зупинка якщо рахув
                StopTimer();

            InitializeGame();
        }

        private void StartTimer() //запуск тайм
        {
            if (gameTimer == null)
            {
                gameTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                gameTimer.Tick += GameTimer_Tick;
            }
            elapsedTime = TimeSpan.Zero;
            UpdateTimeCounter();
            gameTimer.Start();
            isTimerRunning = true;
        }

        private void StopTimer()
        {
            if (gameTimer != null && gameTimer.IsEnabled)
                gameTimer.Stop();
            isTimerRunning = false;
        }

 
        private void GameTimer_Tick(object sender, EventArgs e) //оброб сек
        {
            elapsedTime = elapsedTime.Add(TimeSpan.FromSeconds(1));
            UpdateTimeCounter();
        }

        private void RulesButton_Click(object sender, RoutedEventArgs e) //правила на гру
        {
            MessageBox.Show(
                "Правила гри 'Ханойські вежі':\n\n" +
                "1. Можна переміщати один диск за раз\n" +
                "2. Не можна класти більший диск на менший\n" +
                "3. Диски можна переміщати тільки між стовпцями\n\n" +
                "Мета: Перемістити всі диски на правий стовпець",
                "Правила",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
