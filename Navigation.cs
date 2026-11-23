using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace GymZ
{
    internal sealed partial class GymApp
    {
        private Exercise openGuide;
        private Section workoutRecordOwner = Section.Workouts;

        private void Go(Section page)
        {
            Navigate(page, page == Section.Workouts ? training.Today : selectedDate);
        }

        private void SelectDay(DateTime date, Section page)
        {
            if (page == Section.Workouts && date.Date != training.Today)
            {
                if (section != Section.WorkoutRecord) workoutRecordOwner = section;
                Navigate(Section.WorkoutRecord, date.Date);
            }
            else Navigate(page, date.Date);
        }

        private void Navigate(Section page, DateTime day)
        {
            if (!CanOpen(page)) page = Section.Profile;
            openGuide = null;
            section = page;
            week = Training.Monday(day);
            selectedDate = day;
            Render(false);
        }

        private void OpenExerciseGuide(Exercise exercise)
        {
            openGuide = exercise;
            Render(false);
        }

        private void Home()
        {
            Go(Section.Workouts);
        }

        private bool CanOpen(Section page)
        {
            return !training.NeedsSetup || page == Section.Profile || page == Section.Settings;
        }

        private void GoBack()
        {
            if (!HasTabReturn()) return;
            var root = section == Section.WorkoutRecord ? workoutRecordOwner : section;
            if (training.NeedsSetup) root = Section.Profile;
            Navigate(root, training.Today);
        }

        private bool HasTabReturn()
        {
            return openGuide != null || section == Section.WorkoutRecord ||
                (training.NeedsSetup && section == Section.Settings) ||
                ((section == Section.Overview || section == Section.Attendance || section == Section.Reports) && selectedDate != training.Today);
        }

        private void ShowPageNavigation()
        {
            var actions = Get<WrapPanel>("PageNavigation");
            actions.Children.Clear();
            if (HasTabReturn())
            {
                var back = Button("‹  Voltar", GoBack, "Nav");
                back.MinHeight = 28;
                back.Padding = new Thickness(7, 3, 9, 3);
                back.ToolTip = "Voltar ao início desta aba (Alt+←)";
                AddChoice(actions, back);
            }
            if ((section != Section.Workouts || openGuide != null) && !training.NeedsSetup)
            {
                var home = Button("Início", Home, "Nav");
                home.MinHeight = 28;
                home.Padding = new Thickness(7, 3, 9, 3);
                home.ToolTip = "Abrir o treino de hoje (F1)";
                AddChoice(actions, home);
            }
            actions.Visibility = actions.Children.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        }

        private void WeekControls()
        {
            var previous = Button("‹", () => MoveWeek(-7));
            var next = Button("›", () => MoveWeek(7));
            AutomationProperties.SetName(previous, "Semana anterior");
            AutomationProperties.SetName(next, "Próxima semana");
            headerActions.Children.Add(previous);
            var period = Button(week.ToString("dd/MM") + " – " + week.AddDays(6).ToString("dd/MM") + "  ▾", OpenCalendar);
            period.Margin = new Thickness(5, 0, 5, 0);
            period.ToolTip = "Abrir calendário mensal";
            AutomationProperties.SetName(period, "Abrir calendário mensal, período de " + Range(week) + " a " + Range(week.AddDays(6)));
            headerActions.Children.Add(period);
            headerActions.Children.Add(next);
            if (week != Training.Monday(training.Today) || selectedDate != training.Today)
            {
                bool otherMonth = selectedDate.Month != training.Today.Month || selectedDate.Year != training.Today.Year;
                var current = Button(otherMonth ? "Mês atual" : "Hoje", ResetWeek);
                current.ToolTip = "Voltar à data de hoje (Ctrl+Home)";
                current.Margin = new Thickness(5, 0, 0, 0);
                headerActions.Children.Add(current);
            }
        }

        private void OpenCalendar()
        {
            StackPanel panel;
            var dialog = Dialog("Calendário mensal", out panel);
            DateTime month = new DateTime(selectedDate.Year, selectedDate.Month, 1);
            var title = Text("", 21, null, true);
            panel.Children.Add(title);
            var toolbar = new WrapPanel();
            var grid = new UniformGrid { Columns = 7 };
            Action render = () => {
                title.Text = Training.Pt.TextInfo.ToTitleCase(month.ToString("MMMM yyyy", Training.Pt));
                grid.Children.Clear();
                foreach (string day in Training.DayShort)
                    grid.Children.Add(Center(Text(day, 12, "Muted", true)));
                for (int i = 0; i < Training.Day(month); i++) grid.Children.Add(new Border());
                for (int i = 0; i < DateTime.DaysInMonth(month.Year, month.Month); i++)
                {
                    DateTime day = month.AddDays(i);
                    var status = StatusFor(day, training.Get(day));
                    var select = Button(day.Day.ToString(), () => { dialog.Close(); SelectDay(day, section); }, day == selectedDate ? "Primary" : null);
                    if (day != selectedDate) select.SetResourceReference(Control.ForegroundProperty, status.Brush);
                    select.Margin = new Thickness(2);
                    select.ToolTip = Range(day) + " · " + status.Label;
                    AutomationProperties.SetName(select, select.ToolTip.ToString());
                    grid.Children.Add(select);
                }
            };
            AddChoice(toolbar, Button("‹  Mês anterior", () => { month = month.AddMonths(-1); render(); }));
            AddChoice(toolbar, Button("Mês seguinte  ›", () => { month = month.AddMonths(1); render(); }));
            AddChoice(toolbar, Button("Mês atual", () => { month = new DateTime(training.Today.Year, training.Today.Month, 1); render(); }, "Selected"));
            Add(panel, toolbar, 12);
            Add(panel, grid, 10);
            Add(panel, Text("Selecione um dia para abrir a semana correspondente.", 12, "Muted"), 10);
            Add(panel, Button("Voltar", dialog.Close), 12);
            render();
            dialog.ShowDialog();
        }
    }
}
