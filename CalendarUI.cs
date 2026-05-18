using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using SmartCalendar.Models;
using SmartCalendar.Services;

namespace SmartCalendar
{
    public partial class MainForm : Form
    {
        [DllImport("user32.dll")]
        private static extern bool RegisterHotKey(IntPtr hWnd, int id, int fsModifiers, int vlc);
        [DllImport("user32.dll")]
        private static extern bool UnregisterHotKey(IntPtr hWnd, int id);

        private CustomCalendarForm _calendarForm;

        public MainForm()
        {
            this.Text = "Quick Scheduler";
            this.Size = new Size(350, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            RegisterHotKey(this.Handle, 100, 0x0001, 0x4D);

            Label lblInfo = new Label
            {
                Text = "프로그램 실행 중\n[Alt + M] : 메모 작성\n작성 후 엔터 -> 달력 클릭",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("맑은 고딕", 10, FontStyle.Bold)
            };
            this.Controls.Add(lblInfo);
            _calendarForm = new CustomCalendarForm();
        }

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 100)
            {
                using (MemoForm memoForm = new MemoForm())
                {
                    if (memoForm.ShowDialog() == DialogResult.OK)
                    {
                        _calendarForm.PrepareRegistration(memoForm.NewSchedule);
                        _calendarForm.Show();
                        _calendarForm.BringToFront();
                        _calendarForm.ShowModelessMsg("달력에서 날짜를 클릭하면 일정이 등록됩니다.");
                    }
                }
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            UnregisterHotKey(this.Handle, 100);
            base.OnFormClosing(e);
        }
    }

    public class NotificationForm : Form
    {
        public NotificationForm(string message, Color backColor)
        {
            this.FormBorderStyle = FormBorderStyle.None;
            this.Size = new Size(300, 50);
            this.BackColor = backColor;
            this.ShowInTaskbar = false;
            this.TopMost = true;
            this.StartPosition = FormStartPosition.Manual;

            Label lbl = new Label
            {
                Text = message,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("맑은 고딕", 9, FontStyle.Bold),
                ForeColor = Color.Black
            };
            this.Controls.Add(lbl);

            Timer t = new Timer { Interval = 2000 };
            t.Tick += (s, e) => { this.Close(); t.Stop(); };
            t.Start();
            lbl.Click += (s, e) => this.Close();
        }
    }

    public class MemoForm : Form
    {
        public Schedule NewSchedule { get; private set; }
        private RadioButton rbCar, rbTrain, rbBus;
        private TextBox txtMemo, txtLocation, txtStart, txtEnd;

        public MemoForm()
        {
            this.Text = "메모 작성 (Enter: 날짜 선택)";
            this.Size = new Size(400, 280);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

            Panel p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };

            // 내용 입력
            p.Controls.Add(new Label { Text = "내용:", Location = new Point(20, 15), AutoSize = true });
            txtMemo = new TextBox { Location = new Point(80, 12), Width = 270, Font = new Font("맑은 고딕", 10) };
            p.Controls.Add(txtMemo);

            // 장소 입력
            p.Controls.Add(new Label { Text = "장소:", Location = new Point(20, 50), AutoSize = true });
            txtLocation = new TextBox { Location = new Point(80, 47), Width = 270, Font = new Font("맑은 고딕", 10) };
            p.Controls.Add(txtLocation);

            // 시작 시간
            p.Controls.Add(new Label { Text = "시작:", Location = new Point(20, 85), AutoSize = true });
            txtStart = new TextBox { Location = new Point(80, 82), Width = 120, Font = new Font("맑은 고딕", 10), Text = "09:00" };
            p.Controls.Add(txtStart);

            // 종료 시간
            p.Controls.Add(new Label { Text = "종료:", Location = new Point(220, 85), AutoSize = true });
            txtEnd = new TextBox { Location = new Point(265, 82), Width = 85, Font = new Font("맑은 고딕", 10), Text = "10:00" };
            p.Controls.Add(txtEnd);

            // 이동수단
            p.Controls.Add(new Label { Text = "이동수단:", Location = new Point(20, 120), AutoSize = true });
            rbCar = new RadioButton { Text = "자차", Location = new Point(100, 118), Checked = true, AutoSize = true };
            rbTrain = new RadioButton { Text = "기차", Location = new Point(170, 118), AutoSize = true };
            rbBus = new RadioButton { Text = "버스", Location = new Point(240, 118), AutoSize = true };
            p.Controls.Add(rbCar); p.Controls.Add(rbTrain); p.Controls.Add(rbBus);

            // 확인 버튼
            Button btnOk = new Button { Text = "확인", Location = new Point(150, 160), Width = 80 };
            btnOk.Click += (s, e) => Confirm();
            p.Controls.Add(btnOk);

            this.Controls.Add(p);
        }

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(txtMemo.Text)) { MessageBox.Show("내용을 입력하세요."); return; }

            // 이동수단 결정
            TransportMode mode;
            if (rbTrain.Checked) mode = TransportMode.Train;
            else if (rbBus.Checked) mode = TransportMode.Bus;
            else mode = TransportMode.Car;

            // 시간 파싱
            TimeSpan start, end;
            if (!TimeSpan.TryParse(txtStart.Text, out start) || !TimeSpan.TryParse(txtEnd.Text, out end))
            {
                MessageBox.Show("시간 형식이 올바르지 않습니다.\n예: 09:00");
                return;
            }

            NewSchedule = new Schedule
            {
                StartDateTime = DateTime.Today.Add(start),
                EndDateTime = DateTime.Today.Add(end),
                Location = string.IsNullOrWhiteSpace(txtLocation.Text) ? null : txtLocation.Text.Trim(),
                Memo = txtMemo.Text.Trim(),
                TransportType = rbTrain.Checked ? "기차" : rbBus.Checked ? "버스" : "자차"
            };

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    public class CustomCalendarForm : Form
    {
        private List<Schedule> _allSchedules = new List<Schedule>();
        private TravelService _travelService = new TravelService();
        private TableLayoutPanel grid;
        private Label lblMonth;
        private DateTime _currentViewDate = DateTime.Now;
        private Schedule _pendingSchedule;
        private bool isWaitingForClick = false;

        private Color[] highlightColors = {
            Color.FromArgb(180, Color.Yellow), Color.FromArgb(180, Color.LightGreen),
            Color.FromArgb(180, Color.LightSkyBlue), Color.FromArgb(180, Color.HotPink),
            Color.FromArgb(180, Color.Orange)
        };

        public CustomCalendarForm()
        {
            this.Text = "나의 스케줄러";
            this.Size = new Size(1000, 900);
            this.StartPosition = FormStartPosition.CenterScreen;

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 60 };
            lblMonth = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("맑은 고딕", 16, FontStyle.Bold) };
            Button btnPrev = new Button { Text = "<", Dock = DockStyle.Left, Width = 60 };
            btnPrev.Click += (s, e) => { _currentViewDate = _currentViewDate.AddMonths(-1); RefreshCalendar(); };
            Button btnNext = new Button { Text = ">", Dock = DockStyle.Right, Width = 60 };
            btnNext.Click += (s, e) => { _currentViewDate = _currentViewDate.AddMonths(1); RefreshCalendar(); };
            topPanel.Controls.Add(lblMonth); topPanel.Controls.Add(btnPrev); topPanel.Controls.Add(btnNext);

            grid = new TableLayoutPanel { RowCount = 6, ColumnCount = 7, Dock = DockStyle.Fill };
            for (int i = 0; i < 7; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.28f));
            for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 16.66f));
            this.Controls.Add(grid); this.Controls.Add(topPanel);
            RefreshCalendar();
        }

        public void ShowModelessMsg(string msg, bool isSuccess = false)
        {
            NotificationForm nav = new NotificationForm(msg, isSuccess ? Color.LightGreen : Color.LightSkyBlue);
            nav.Location = new Point(this.Location.X + (this.Width / 2) - 150, this.Location.Y + 80);
            nav.Show(this);
        }

        public void PrepareRegistration(Schedule schedule)
        {
            _pendingSchedule = schedule;
            isWaitingForClick = true;
        }

        private void RefreshCalendar()
        {
            grid.Controls.Clear();
            lblMonth.Text = _currentViewDate.ToString("yyyy년 MM월");
            DateTime firstDay = new DateTime(_currentViewDate.Year, _currentViewDate.Month, 1);
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_currentViewDate.Year, _currentViewDate.Month);

            for (int i = 0; i < startDayOfWeek; i++) grid.Controls.Add(new Label());

            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime dateSlot = new DateTime(_currentViewDate.Year, _currentViewDate.Month, day);
                var todaySchedules = _allSchedules.Where(s => s.StartDateTime.Date == dateSlot.Date).ToList();

                Button btn = new Button
                {
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = new Font("맑은 고딕", 9),
                    BackColor = (dateSlot.Date == DateTime.Today) ? Color.LemonChiffon : Color.White,
                    Text = day.ToString()
                };

                btn.Paint += (s, e) => {
                    if (todaySchedules.Count == 0) return;
                    Graphics g = e.Graphics;
                    float startY = 25;
                    for (int i = 0; i < Math.Min(todaySchedules.Count, 4); i++)
                    {
                        string content = "• " + todaySchedules[i].Memo;
                        SizeF textSize = g.MeasureString(content, btn.Font);
                        using (SolidBrush brush = new SolidBrush(highlightColors[i % highlightColors.Length]))
                            g.FillRectangle(brush, new RectangleF(5, startY + 2, textSize.Width, textSize.Height - 4));
                        g.DrawString(content, btn.Font, Brushes.Black, 5, startY);
                        startY += textSize.Height + 2;
                    }
                };

                btn.Click += (s, e) => {
                    if (isWaitingForClick)
                    {
                        // TransportType → TransportMode 변환
                        TransportMode mode;
                        if (_pendingSchedule.TransportType == "기차")
                            mode = TransportMode.Train;
                        else if (_pendingSchedule.TransportType == "버스")
                            mode = TransportMode.Bus;
                        else
                            mode = TransportMode.Car;

                        // 날짜 반영
                        Schedule newSched = new Schedule
                        {
                            StartDateTime = dateSlot.Date.Add(_pendingSchedule.StartDateTime.TimeOfDay),
                            EndDateTime = dateSlot.Date.Add(_pendingSchedule.EndDateTime.TimeOfDay),
                            Location = _pendingSchedule.Location,
                            Memo = _pendingSchedule.Memo,
                            TransportType = _pendingSchedule.TransportType
                        };
                        bool ok = _travelService.CanInsert(newSched, _allSchedules, mode);

                        // 네 핵심 로직 연결
                        // 이동수단은 MemoForm에서 받아야 하는데 일단 Car 기본값


                        if (ok)
                        {
                            _allSchedules.Add(newSched);
                            isWaitingForClick = false;
                            RefreshCalendar();
                            ShowModelessMsg($"{dateSlot:MM/dd} 등록 완료!", true);
                        }
                        else
                        {
                            DateTime? suggest = _travelService.SuggestEarliestStart(newSched, _allSchedules, mode);
                            string suggestStr = suggest.HasValue ? suggest.Value.ToString("HH:mm") + " 이후 가능" : "추천 불가";
                            ShowModelessMsg($"이동시간 부족! 💡 {suggestStr}");
                        }
                    }
                    else if (todaySchedules.Count > 0)
                    {
                        string msg = string.Join("\n\n", todaySchedules.Select(x =>
                            $"{x.StartDateTime:HH:mm}~{x.EndDateTime:HH:mm} {x.Location ?? "장소없음"}\n{x.Memo}"));
                        MessageBox.Show($"{dateSlot.ToLongDateString()} 상세 일정:\n\n{msg}");
                    }
                };
                grid.Controls.Add(btn);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; this.Hide(); }
            base.OnFormClosing(e);
        }
    }
}