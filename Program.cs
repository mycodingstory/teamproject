using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Team3
{
    public class ScheduleItem
    {
        public DateTime TargetDate { get; set; }
        public string Description { get; set; }
        public string Transport { get; set; }
        public override string ToString() => $"{Transport}\n- {Description}";
        public string ToShortString() => $"{Transport} {Description}";
    }

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
                        _calendarForm.PrepareRegistration(memoForm.Transport, memoForm.Description);
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
        public string Transport { get; private set; }
        public string Description { get; private set; }
        private RadioButton rbCar, rbPublic;
        private TextBox txt;
        private Panel p;

        public MemoForm(string existingTransport = "", string existingDescription = "")
        {
            bool isEditMode = !string.IsNullOrEmpty(existingDescription);

            this.Text = !isEditMode ? "메모 작성 (Enter: 날짜 선택)" : "메모 수정 (Enter: 완료)";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

            p = new Panel { Dock = DockStyle.Fill, Padding = new Padding(15) };
            rbCar = new RadioButton { Text = "자차", Location = new Point(100, 18), AutoSize = true };
            rbPublic = new RadioButton { Text = "대중교통", Location = new Point(170, 18), AutoSize = true };

            p.Controls.Add(new Label { Text = "이동수단:", Location = new Point(20, 20), AutoSize = true });
            p.Controls.Add(rbCar);
            p.Controls.Add(rbPublic);

            txt = new TextBox { Multiline = true, Dock = DockStyle.Top, Height = 80, Font = new Font("맑은 고딕", 12), Text = existingDescription };
            txt.KeyDown += (s, e) => {
                if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; Confirm(); }
            };

            this.Controls.Add(p);
            this.Controls.Add(txt);

            if (existingTransport == "[대중교통]")
            {
                rbPublic.Checked = true;
            }
            else
            {
                rbCar.Checked = true;
            }

            if (isEditMode)
            {
                this.Size = new Size(400, 125);
                p.Visible = false;
                this.Transport = existingTransport;
            }
            else
            {
                this.Size = new Size(400, 180);
                p.Visible = true;
            }
        }

        private void Confirm()
        {
            if (string.IsNullOrWhiteSpace(txt.Text)) { MessageBox.Show("내용을 입력하세요."); return; }

            if (p.Visible)
            {
                Transport = rbCar.Checked ? "[자차]" : "[대중교통]";
            }

            Description = txt.Text.Trim();
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }

    public class ScheduleDetailForm : Form
    {
        private List<ScheduleItem> _daySchedules;
        private List<ScheduleItem> _allSchedules;
        private ListBox lbSchedules;
        private Button btnEdit, btnDelete, btnClose;

        public ScheduleDetailForm(DateTime date, List<ScheduleItem> daySchedules, List<ScheduleItem> allSchedules)
        {
            this._daySchedules = daySchedules;
            this._allSchedules = allSchedules;

            this.Text = $"{date.ToLongDateString()} 상세 일정";
            this.Size = new Size(400, 300);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            lbSchedules = new ListBox { Dock = DockStyle.Top, Height = 180, Font = new Font("맑은 고딕", 10) };
            UpdateListBox();

            Panel btnPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10) };
            btnEdit = new Button { Text = "수정", Location = new Point(40, 15), Size = new Size(90, 35) };
            btnDelete = new Button { Text = "삭제", Location = new Point(145, 15), Size = new Size(90, 35), BackColor = Color.MistyRose };
            btnClose = new Button { Text = "닫기", Location = new Point(250, 15), Size = new Size(90, 35) };

            btnEdit.Click += BtnEdit_Click;
            btnDelete.Click += BtnDelete_Click;
            btnClose.Click += (s, e) => this.Close();

            btnPanel.Controls.AddRange(new Control[] { btnEdit, btnDelete, btnClose });
            this.Controls.AddRange(new Control[] { btnPanel, lbSchedules });
        }

        private void UpdateListBox()
        {
            lbSchedules.Items.Clear();
            foreach (var item in _daySchedules)
            {
                lbSchedules.Items.Add(item.ToShortString());
            }
        }

        private void BtnEdit_Click(object sender, EventArgs e)
        {
            int index = lbSchedules.SelectedIndex;
            if (index < 0) { MessageBox.Show("수정할 일정을 선택해주세요."); return; }

            ScheduleItem selectedItem = _daySchedules[index];

            using (MemoForm memoForm = new MemoForm(selectedItem.Transport, selectedItem.Description))
            {
                if (memoForm.ShowDialog() == DialogResult.OK)
                {
                    selectedItem.Transport = memoForm.Transport;
                    selectedItem.Description = memoForm.Description;
                    UpdateListBox();
                    MessageBox.Show("일정이 수정되었습니다.");
                }
            }
        }

        private void BtnDelete_Click(object sender, EventArgs e)
        {
            int index = lbSchedules.SelectedIndex;
            if (index < 0) { MessageBox.Show("삭제할 일정을 선택해주세요."); return; }

            if (MessageBox.Show("선택한 일정을 삭제하시겠습니까?", "삭제 확인", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                ScheduleItem selectedItem = _daySchedules[index];
                _allSchedules.Remove(selectedItem);
                _daySchedules.RemoveAt(index);

                UpdateListBox();
                MessageBox.Show("일정이 삭제되었습니다.");
            }
        }
    }

    public class DateSelectForm : Form
    {
        public int SelectedYear { get; private set; }
        public int SelectedMonth { get; private set; }

        public DateSelectForm(int currentYear, int currentMonth)
        {
            this.Text = "년/월 직접 이동";
            this.Size = new Size(260, 150);
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedToolWindow;

            ComboBox cbYear = new ComboBox { Location = new Point(20, 25), Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
            ComboBox cbMonth = new ComboBox { Location = new Point(130, 25), Width = 70, DropDownStyle = ComboBoxStyle.DropDownList };

            for (int y = currentYear - 30; y <= currentYear + 30; y++) cbYear.Items.Add(y + "년");
            for (int m = 1; m <= 12; m++) cbMonth.Items.Add(m + "월");

            cbYear.SelectedItem = currentYear + "년";
            cbMonth.SelectedItem = currentMonth + "월";

            Button btnOk = new Button { Text = "이동", Location = new Point(45, 70), Size = new Size(70, 28) };
            Button btnCancel = new Button { Text = "취소", Location = new Point(125, 70), Size = new Size(70, 28) };

            btnOk.Click += (s, e) => {
                SelectedYear = int.Parse(cbYear.SelectedItem.ToString().Replace("년", ""));
                SelectedMonth = int.Parse(cbMonth.SelectedItem.ToString().Replace("월", ""));
                this.DialogResult = DialogResult.OK;
                this.Close();
            };
            btnCancel.Click += (s, e) => this.Close();

            this.Controls.AddRange(new Control[] { cbYear, cbMonth, btnOk, btnCancel });
        }
    }

    public class CustomCalendarForm : Form
    {
        private List<ScheduleItem> _allSchedules = new List<ScheduleItem>();
        private TableLayoutPanel grid;
        private Label lblMonth;
        private DateTime _currentViewDate = DateTime.Now;
        private string pendingTransport, pendingDescription;
        private bool isWaitingForClick = false;

        private KoreanLunisolarCalendar k_calendar = new KoreanLunisolarCalendar();

        // 🛠 [오타 수정 완료] Color.Yellow 앞에 누락되었던 점(.)을 찍었습니다.
        private Color[] highlightColors = {
            Color.FromArgb(180, Color.Yellow), Color.FromArgb(180, Color.LightGreen),
            Color.FromArgb(180, Color.LightSkyBlue), Color.FromArgb(180, Color.HotPink), Color.FromArgb(180, Color.Orange)
        };

        private readonly string[] dayNames = { "일", "월", "화", "수", "목", "금", "토" };

        public CustomCalendarForm()
        {
            this.Text = "나의 스케줄러";
            this.Size = new Size(1000, 900);
            this.StartPosition = FormStartPosition.CenterScreen;

            Panel topPanel = new Panel { Dock = DockStyle.Top, Height = 60 };

            lblMonth = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("맑은 고딕", 16, FontStyle.Bold),
                Cursor = Cursors.Hand
            };

            lblMonth.Click += LblMonth_Click;

            Button btnPrev = new Button { Text = "<", Dock = DockStyle.Left, Width = 60 };
            btnPrev.Click += (s, e) => { _currentViewDate = _currentViewDate.AddMonths(-1); RefreshCalendar(); };
            Button btnNext = new Button { Text = ">", Dock = DockStyle.Right, Width = 60 };
            btnNext.Click += (s, e) => { _currentViewDate = _currentViewDate.AddMonths(1); RefreshCalendar(); };
            topPanel.Controls.Add(lblMonth); topPanel.Controls.Add(btnPrev); topPanel.Controls.Add(btnNext);

            grid = new TableLayoutPanel { RowCount = 7, ColumnCount = 7, Dock = DockStyle.Fill };
            for (int i = 0; i < 7; i++) grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 14.28f));

            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 8f));
            for (int i = 0; i < 6; i++) grid.RowStyles.Add(new RowStyle(SizeType.Percent, 15.33f));

            this.Controls.Add(grid); this.Controls.Add(topPanel);
            RefreshCalendar();
        }

        private void LblMonth_Click(object sender, EventArgs e)
        {
            using (DateSelectForm dsForm = new DateSelectForm(_currentViewDate.Year, _currentViewDate.Month))
            {
                if (dsForm.ShowDialog() == DialogResult.OK)
                {
                    _currentViewDate = new DateTime(dsForm.SelectedYear, dsForm.SelectedMonth, 1);
                    RefreshCalendar();
                }
            }
        }

        public void ShowModelessMsg(string msg, bool isSuccess = false)
        {
            NotificationForm nav = new NotificationForm(msg, isSuccess ? Color.LightGreen : Color.LightSkyBlue);
            nav.Location = new Point(this.Location.X + (this.Width / 2) - 150, this.Location.Y + 80);
            nav.Show(this);
        }

        public void PrepareRegistration(string transport, string description)
        {
            this.pendingTransport = transport;
            this.pendingDescription = description;
            this.isWaitingForClick = true;
        }

        private string GetKoreanHolidayName(DateTime date)
        {
            if (date.Month == 1 && date.Day == 1) return "신정";
            if (date.Month == 3 && date.Day == 1) return "삼일절";
            if (date.Month == 5 && date.Day == 5) return "어린이날";
            if (date.Month == 6 && date.Day == 6) return "현충일";
            if (date.Month == 8 && date.Day == 15) return "광복절";
            if (date.Month == 10 && date.Day == 3) return "개천절";
            if (date.Month == 10 && date.Day == 9) return "한글날";
            if (date.Month == 12 && date.Day == 25) return "성탄절";

            try
            {
                int lYear = k_calendar.GetYear(date);
                int lMonth = k_calendar.GetMonth(date);
                int lDay = k_calendar.GetDayOfMonth(date);
                bool isLeap = k_calendar.IsLeapMonth(lYear, lMonth);

                if (!isLeap)
                {
                    if (lMonth == 1 && lDay == 1) return "설날";
                    if (lMonth == 1 && lDay == 2) return "설연휴";

                    DateTime tomorrow = date.AddDays(1);
                    if (k_calendar.GetMonth(tomorrow) == 1 && k_calendar.GetDayOfMonth(tomorrow) == 1 && !k_calendar.IsLeapMonth(k_calendar.GetYear(tomorrow), k_calendar.GetMonth(tomorrow)))
                        return "설연휴";

                    if (lMonth == 4 && lDay == 8) return "부처님오신날";

                    if (lMonth == 8 && lDay == 14) return "추석연휴";
                    if (lMonth == 8 && lDay == 15) return "추석";
                    if (lMonth == 8 && lDay == 16) return "추석연휴";
                }

                if (date.DayOfWeek == DayOfWeek.Monday)
                {
                    string prevName = GetMainHolidayName(date.AddDays(-1));
                    if (prevName != "" && date.AddDays(-1).DayOfWeek == DayOfWeek.Sunday) return "대체공휴일";

                    string prev2Name = GetMainHolidayName(date.AddDays(-2));
                    if (prev2Name != "" && date.AddDays(-2).DayOfWeek == DayOfWeek.Saturday) return "대체공휴일";
                }
                else if (date.DayOfWeek == DayOfWeek.Tuesday)
                {
                    if (GetMainHolidayName(date.AddDays(-1)) != "" && GetMainHolidayName(date.AddDays(-2)) != "" && date.AddDays(-2).DayOfWeek == DayOfWeek.Sunday)
                        return "대체공휴일";
                }
            }
            catch { }

            return "";
        }

        private string GetMainHolidayName(DateTime date)
        {
            if (date.Month == 3 && date.Day == 1) return "삼일절";
            if (date.Month == 5 && date.Day == 5) return "어린이날";
            if (date.Month == 8 && date.Day == 15) return "광복절";
            if (date.Month == 10 && date.Day == 3) return "개천절";
            if (date.Month == 10 && date.Day == 9) return "한글날";
            if (date.Month == 12 && date.Day == 25) return "성탄절";

            try
            {
                int lMonth = k_calendar.GetMonth(date);
                int lDay = k_calendar.GetDayOfMonth(date);
                if (!k_calendar.IsLeapMonth(k_calendar.GetYear(date), lMonth))
                {
                    if (lMonth == 1 && (lDay == 1 || lDay == 2)) return "설날";
                    DateTime tom = date.AddDays(1);
                    if (k_calendar.GetMonth(tom) == 1 && k_calendar.GetDayOfMonth(tom) == 1) return "설날";
                    if (lMonth == 4 && lDay == 8) return "부처님오신날";
                    if (lMonth == 8 && (lDay == 14 || lDay == 15 || lDay == 16)) return "추석";
                }
            }
            catch { }
            return "";
        }

        private void RefreshCalendar()
        {
            grid.Controls.Clear();
            lblMonth.Text = _currentViewDate.ToString("yyyy년 MM월");

            for (int i = 0; i < 7; i++)
            {
                Label lblDayOfWeek = new Label
                {
                    Text = dayNames[i],
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleCenter,
                    Font = new Font("맑은 고딕", 11, FontStyle.Bold),
                    BackColor = Color.FromArgb(245, 245, 245)
                };

                if (i == 0) lblDayOfWeek.ForeColor = Color.Red;
                else if (i == 6) lblDayOfWeek.ForeColor = Color.Blue;
                else lblDayOfWeek.ForeColor = Color.Black;

                grid.Controls.Add(lblDayOfWeek);
            }

            DateTime firstDay = new DateTime(_currentViewDate.Year, _currentViewDate.Month, 1);
            int startDayOfWeek = (int)firstDay.DayOfWeek;
            int daysInMonth = DateTime.DaysInMonth(_currentViewDate.Year, _currentViewDate.Month);

            for (int i = 0; i < startDayOfWeek; i++) grid.Controls.Add(new Label());

            for (int day = 1; day <= daysInMonth; day++)
            {
                DateTime dateSlot = new DateTime(_currentViewDate.Year, _currentViewDate.Month, day);
                var todaySchedules = _allSchedules.Where(s => s.TargetDate.Date == dateSlot.Date).ToList();

                Color cellBackColor = (dateSlot.Date == DateTime.Today) ? Color.LemonChiffon : Color.White;

                string holidayName = GetKoreanHolidayName(dateSlot);
                bool isHoliday = !string.IsNullOrEmpty(holidayName);

                Color cellForeColor = Color.Black;
                if (dateSlot.DayOfWeek == DayOfWeek.Sunday || isHoliday)
                {
                    cellForeColor = Color.Red;
                }
                else if (dateSlot.DayOfWeek == DayOfWeek.Saturday)
                {
                    cellForeColor = Color.Blue;
                }

                string buttonText = day.ToString();
                if (isHoliday)
                {
                    buttonText = $"{day}  {holidayName}";
                }

                Button btn = new Button
                {
                    Dock = DockStyle.Fill,
                    FlatStyle = FlatStyle.Flat,
                    TextAlign = ContentAlignment.TopLeft,
                    Font = new Font("맑은 고딕", 9, FontStyle.Bold),
                    BackColor = cellBackColor,
                    ForeColor = cellForeColor,
                    Text = buttonText,
                    // 여백(Padding)을 줘서 글자가 외곽선에 닿지 않도록 안전거리 확보
                    Padding = new Padding(3, 3, 0, 0)
                };

                btn.FlatAppearance.BorderColor = Color.DarkGray;

                btn.Paint += (s, e) => {
                    if (todaySchedules.Count == 0) return;
                    Graphics g = e.Graphics;
                    float startY = 25;
                    for (int i = 0; i < Math.Min(todaySchedules.Count, 4); i++)
                    {
                        string content = "• " + todaySchedules[i].ToShortString();
                        Font regularFont = new Font("맑은 고딕", 9, FontStyle.Regular);
                        SizeF textSize = g.MeasureString(content, regularFont);
                        using (SolidBrush brush = new SolidBrush(highlightColors[i % highlightColors.Length]))
                        {
                            g.FillRectangle(brush, new RectangleF(5, startY + 2, textSize.Width, textSize.Height - 4));
                        }
                        g.DrawString(content, regularFont, Brushes.Black, 5, startY);
                        startY += textSize.Height + 2;
                    }
                };

                btn.Click += (s, e) => {
                    if (isWaitingForClick)
                    {
                        _allSchedules.Add(new ScheduleItem
                        {
                            TargetDate = dateSlot.Date,
                            Transport = pendingTransport,
                            Description = pendingDescription
                        });
                        isWaitingForClick = false;
                        RefreshCalendar();
                        ShowModelessMsg($"{dateSlot:MM/dd} 등록 완료!", true);
                    }
                    else if (todaySchedules.Count > 0)
                    {
                        using (ScheduleDetailForm detailForm = new ScheduleDetailForm(dateSlot, todaySchedules, _allSchedules))
                        {
                            detailForm.ShowDialog();
                        }
                        RefreshCalendar();
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

    static class AppRunner
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}