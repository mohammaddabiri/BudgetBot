using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//using ZedGraph;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms.DataVisualization.Charting;

public static class Extensions
{
    public static bool Is(this string text, params string[] list)
    {
        foreach(var item in list)
        {
            if (item == text)
                return true;
        }

        return false;
    }

    public static void SafeInvoke(this Action action)
    {
        if (action != null)
        {
            action.Invoke();
        }
    }

    public static void SafeInvoke<T>(this Action<T> action, T arg)
    {
        if(action != null)
        {
            action.Invoke(arg);
        }
    }
}

public class DeleteBudgetCommand : Command
{
    public string Category;

    public DeleteBudgetCommand(string category)
    {
        Category = category;
    }

    public override Task Execute(DateTime timestamp)
    {
        if(Endpoints.RemoveBudgetCategory(Category))
        {
            Message("Removed budget");
        }

        return null;
    }
}
public class AllocateBudgetCommand : Command
{
    public string Category;
    public float Amount;
    DateTime StartDate;
    BudgetInterval Interval;

    public AllocateBudgetCommand(string category, float amount, DateTime startDate, BudgetInterval interval)
    {
        Category = category;
        Amount = amount;
        StartDate = startDate;
        Interval = interval;
    }

    public override Task Execute(DateTime timestamp)
    {
        Endpoints.AddBudgetCategory(Category, Amount, StartDate, Interval);
        var successMsg = string.Format("Category \"{0}\" added.", Category);
        Message(successMsg);
        return null;
    }
}

public class ClearListCommand : Command
{
    public string Category;

    public ClearListCommand(string category)
    {
        Category = category;
    }

    public override Task Execute(DateTime timestamp)
    {
        Endpoints.ClearBudgetTransactions(Category);        
        return base.Execute(timestamp);
    }

    string FormatTransaction(BudgetTransaction transaction)
    {
        var noteFormatting = !string.IsNullOrEmpty(transaction.Notes) ? "{0}: {1} - {2}" : "{0}: {1}";
        //if (!string.IsNullOrEmpty(transaction.Notes))
        //{
        return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C").PadRight(8), transaction.Notes);
        //}

        //return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C"));
    }
}

public class ListCommand : Command
{
    public string Category;

    public ListCommand(string category)
    {
        Category = category;
    }

    public override Task Execute(DateTime timestamp)
    {
        var allTransactions = Endpoints.FetchBudgetTransactions(Category);
        if(allTransactions == null || allTransactions.Count == 0)
        {
            Message("No transactions recorded.");
            return null;
        }
                
        var transactionMessageBuilder = new StringBuilder();
        foreach (var transaction in allTransactions.OrderByDescending(x => x.Timestamp))
        {
            var transactionMessage = FormatTransaction(transaction);
            transactionMessageBuilder.AppendLine(transactionMessage);
            
        }
        if(transactionMessageBuilder.Length != 0)
        {
            Message(transactionMessageBuilder.ToString());
        }

        return base.Execute(timestamp);
    }

    string FormatTransaction(BudgetTransaction transaction)
    {
        var noteFormatting = !string.IsNullOrEmpty(transaction.Notes) ? "{0}  \t  {1}\t {2}" : "{0}  \t  {1}\t";
        //if (!string.IsNullOrEmpty(transaction.Notes))
        //{
        return string.Format(noteFormatting, transaction.Timestamp.ToString("MM'/'dd'/'yy HH':'mm"), transaction.Cost.ToString("C").PadRight(8), transaction.Notes);
        //}

        //return string.Format(noteFormatting, transaction.Timestamp.ToShortDateString(), transaction.Cost.ToString("C"));
    }
}

public abstract class Command
{
    public event Action<string> OnMessage;
    public BudgetEndpoints Endpoints;
    public virtual async Task Execute(DateTime timestamp) { }
    public void Message(string text)
    {
        OnMessage.SafeInvoke(text);
    }
}

public interface ICommandParser { }
public abstract class CommandParser : ICommandParser
{
    public event Action<string> OnMessage;
    public BudgetEndpoints Endpoints;
    public abstract Command Parse(CommandLineArg[] args);
    public void Message(string text)
    {
        OnMessage.SafeInvoke(text);
    }
}

public class ReportBudgetCommand : Command
{
    public string Category = string.Empty;

    public ReportBudgetCommand() { }
    public ReportBudgetCommand(string category) { Category = category; }

    public override Task Execute(DateTime timestamp)
    {
        var budgetList = Endpoints.FetchBudgetList();
        if (budgetList == null || budgetList.Categories == null || budgetList.Categories.Count == 0)
        {
            Message("No budgets defined.");
            return null;
        }


        if (string.IsNullOrEmpty(Category))
        {
            foreach (var budgetCategory in budgetList.Categories)
            {
                var balance = Endpoints.GetBalance(budgetCategory.Name);
                var message1 = FormatCategoryReport(budgetCategory, balance);
                Message(message1);
            }
        }
        else
        {
            var category = budgetList[Category];
            if (category == null)
            {
                Message(string.Format("Category \"{0}\" not found.", Category));
                return null;
            }

            var balance = Endpoints.GetBalance(category.Name);
            var catMessage1 = FormatCategoryReport(category, balance);
            Message(catMessage1);
        }
        return null;
    }

    public static string FormatCategoryReport(BudgetCategory category, float balance)
    {
        var spendingPct = balance / category.Limit;
        var remainingDays = category.Period.RemainingDays;
        return string.Format("{0}\n{1} / {2} ({3}) {4} days", category.Name.PadRight(16), balance.ToString("C"), category.Limit.ToString("C"), spendingPct.ToString("P0"), remainingDays.ToString().PadLeft(4));
    }
}

public class AddTransactionCommand : Command
{
    public string Category;
    public float Cost;
    public string Notes;
    public DateTime Timestamp = DateTime.Now;

    public AddTransactionCommand()
    {
    }

    public AddTransactionCommand(string category, float cost, DateTime timestamp, string notes = "")
    {
        Category = category;
        Cost = cost;
        Timestamp = timestamp;
        Notes = notes;
    }

    public override Task Execute(DateTime timestamp)
    {
        var budgetList = Endpoints.FetchBudgetList();
        if (budgetList == null)
        {
            Message("No budgets allocated.");
        }
        else
        {
            var category = budgetList[Category];
            if (category == null)
            {
                var errorMessage = string.Format("No category \"{0}\" defined.", Category);
                Message(errorMessage);
            }
            else
            {
                var newTransaction = Endpoints.AddTransaction(Category, Cost, Timestamp, Notes);
                if (newTransaction == null)
                {
                    Message("Failed to log transaction");
                }
                else
                {
                    var allTransactions = Endpoints.FetchBudgetTransactions(Category);
                    var allExpenses = allTransactions.Where(o => o.Timestamp.CompareTo(category.Period.StartDate) > 0).Sum(x => x.Cost);
                    var newBalance = category.Limit - allExpenses;
                    var balanceMessage = string.Format("Balance: {0}", newBalance.ToString("C"));
                    //var message2 = string.Format("{0} days remaining", budgetCategory.Days);
                    Message(balanceMessage);                    
                }
            }
        }
        return null;
    }
}

public class AddTransactionCommandParser : CommandParser
{
    public override Command Parse(CommandLineArg[] args)
    {
        // food 1.99 12/06 greggs
        var withNoteWithDate = args.Length > 3 && args[0].IsAlpha && args[1].IsNumeric && args[2].IsDate;
        if (withNoteWithDate)
        {
            var notesBuilder = new StringBuilder();
            for (var i = 3; i < args.Length; ++i)
            {
                notesBuilder.Append(args[i]);
                notesBuilder.Append(" ");
            }
            return new AddTransactionCommand(args[0], args[1].AsFloat, args[2].AsDate, notesBuilder.ToString());
        }
        // food 1.99 12/06
        var noNoteWithDate = args.Length == 3 && args[0].IsAlpha && args[1].IsNumeric && args[2].IsDate;
        if (noNoteWithDate)
        {
            return new AddTransactionCommand(args[0], args[1].AsFloat, args[2].AsDate);
        }
        // food 1.99 greggs
        var withNoteNoDate = args.Length > 2 && args[0].IsAlpha && args[1].IsNumeric;
        if (withNoteNoDate)
        {
            var notesBuilder = new StringBuilder();
            for (var i = 2; i < args.Length; ++i)
            {
                notesBuilder.Append(args[i]);
                notesBuilder.Append(" ");
            }
            return new AddTransactionCommand(args[0], args[1].AsFloat, DateTime.Now, notesBuilder.ToString());
        }
        // food 1.99
        var noNoteNoDate = args.Length == 2 && args[0].IsAlpha && args[1].IsNumeric;
        if (noNoteNoDate)
        {
            return new AddTransactionCommand(args[0], args[1].AsFloat, DateTime.Now);
        }
        return null;
    }
}

public class VisualiseBudgetCommand : Command
{
    public string Category;

    public VisualiseBudgetCommand(string category)
    {
        Category = category;
    }

    private void CreateGraph(GraphData<DateTime, float> data, string filePath)
    {
        // set up some data
        var xvals = data.X.ToArray();
        var yvals = data.Y.ToArray();

        var startingY = yvals[0];
        var linearY = new float[yvals.Length];
        var todayY = new float[yvals.Length];
        for (var i = 0; i < xvals.Length; ++i)
        {
            if(data.X[i].Date == DateTime.Today)
            {
                todayY[i] = startingY;
            }
            var xPct = i / (float)xvals.Length;
            linearY[i] = startingY * (1f-xPct);
        }
                        
        // create the chart
        var chart = new Chart();
        chart.Size = new Size(600, 250);

        var chartArea = new ChartArea();
        chartArea.AxisX.LabelStyle.Format = "dd/MMM";
        chartArea.AxisX.MajorGrid.LineColor = Color.LightGray;
        chartArea.AxisY.MajorGrid.LineColor = Color.LightGray;
        chartArea.AxisX.LabelStyle.Font = new Font("Consolas", 8);
        chartArea.AxisY.LabelStyle.Font = new Font("Consolas", 8);        
        chart.ChartAreas.Add(chartArea);
                
        var series = new Series();
        series.Name = "Series1";
        series.ChartType = SeriesChartType.FastLine;
        series.XValueType = ChartValueType.DateTime;
        chart.Series.Add(series);

        //var series3 = new Series();
        //series.Name = "Series3";
        //series.ChartType = SeriesChartType.Spline;
        //series.XValueType = ChartValueType.DateTime;
        //chart.Series.Add(series3);

        // bind the datapoints
        chart.Series["Series1"].Points.DataBindXY(xvals, yvals);

        // copy the series and manipulate the copy
        chart.DataManipulator.CopySeriesValues("Series1", "Series2");
        chart.DataManipulator.FinancialFormula(
            FinancialFormula.WeightedMovingAverage,
            "Series2"
        );
        chart.Series["Series2"].ChartType = SeriesChartType.FastLine;

        //chart.Series["Series3"].Points.DataBindXY(xvals, yvals);
        // copy the series and manipulate the copy
        chart.DataManipulator.CopySeriesValues("Series1", "Series3");
        chart.DataManipulator.FinancialFormula(
            FinancialFormula.MovingAverage,
            "Series3"
        );

        chart.Series["Series3"].Points.DataBindXY(xvals, linearY);
        chart.Series["Series3"].ChartType = SeriesChartType.Spline;

        var todaySeries = new Series();
        todaySeries.Name = "TodaySeries";
        todaySeries.ChartType = SeriesChartType.BoxPlot;
        todaySeries.XValueType = ChartValueType.DateTime;
        todaySeries.Points.DataBindXY(xvals, todayY);
        chart.Series.Add(todaySeries);

        // draw!
        chart.Invalidate();

        // write out a file
        chart.SaveImage(filePath, ChartImageFormat.Png);
    }
    //// Build the Chart
    //private void CreateGraph(ref GraphPane myPane)
    //{
    //    // Set the Titles
    //    myPane.Title.Text = "My Test Bar Graph";
    //    myPane.XAxis.Title.Text = "Label";
    //    myPane.YAxis.Title.Text = "My Y Axis";

    //    // Make up some random data points
    //    string[] labels = { "Panther", "Lion", "Cheetah",
    //                  "Cougar", "Tiger", "Leopard" };
    //    double[] y = { 100, 115, 75, 22, 98, 40 };
    //    double[] y2 = { 90, 100, 95, 35, 80, 35 };
    //    double[] y3 = { 80, 110, 65, 15, 54, 67 };
    //    double[] y4 = { 120, 125, 100, 40, 105, 75 };

    //    // Generate a red bar with "Curve 1" in the legend
    //    BarItem myBar = myPane.AddBar("Curve 1", null, y,
    //                                                Color.Red);
    //    myBar.Bar.Fill = new Fill(Color.Red, Color.White,
    //                                                Color.Red);

    //    // Generate a blue bar with "Curve 2" in the legend
    //    myBar = myPane.AddBar("Curve 2", null, y2, Color.Blue);
    //    myBar.Bar.Fill = new Fill(Color.Blue, Color.White,
    //                                                Color.Blue);

    //    // Generate a green bar with "Curve 3" in the legend
    //    myBar = myPane.AddBar("Curve 3", null, y3, Color.Green);
    //    myBar.Bar.Fill = new Fill(Color.Green, Color.White,
    //                                                Color.Green);

    //    // Generate a black line with "Curve 4" in the legend
    //    LineItem myCurve = myPane.AddCurve("Curve 4",
    //          null, y4, Color.Black, SymbolType.Circle);
    //    myCurve.Line.Fill = new Fill(Color.White,
    //                          Color.LightSkyBlue, -45F);

    //    // Fix up the curve attributes a little
    //    myCurve.Symbol.Size = 8.0F;
    //    myCurve.Symbol.Fill = new Fill(Color.White);
    //    myCurve.Line.Width = 2.0F;

    //    // Draw the X tics between the labels instead of 
    //    // at the labels
    //    myPane.XAxis.MajorTic.IsBetweenLabels = true;

    //    // Set the XAxis labels
    //    myPane.XAxis.Scale.TextLabels = labels;
    //    // Set the XAxis to Text type
    //    myPane.XAxis.Type = AxisType.Text;

    //    // Fill the Axis and Pane backgrounds
    //    myPane.Chart.Fill = new Fill(Color.White,
    //          Color.FromArgb(255, 255, 166), 90F);
    //    myPane.Fill = new Fill(Color.FromArgb(250, 250, 255));

    //    // Tell ZedGraph to refigure the
    //    // axes since the data have changed
    //    //zg1.AxisChange(
    //}
    public override Task Execute(DateTime timestamp)
    {
        var allTransactions = Endpoints.FetchBudgetTransactions(Category);
        var category = Endpoints.GetBudgetCategory(Category);
        var allExpenses = allTransactions.Where(o => o.Timestamp.CompareTo(category.Period.StartDate) > 0);

        var graphData = BuildBudgetGraph(category, allExpenses.ToList());        
        var tempFile = System.IO.Path.GetTempFileName() + ".png";
        CreateGraph(graphData, tempFile);
        Message("image://" + tempFile);

        return null;
    }
    
    public class GraphData<Tx,Ty>
    {
        public List<Tx> X = new List<Tx>();
        public List<Ty> Y = new List<Ty>();

        public GraphData()
        {
        }

        public void Add(Tx x, Ty y)
        {
            X.Add(x);
            Y.Add(y);
        }
    }

    public struct DayBalance
    {
        public DateTime Date;
        public float Balance;
    }

    private GraphData<DateTime, float> BuildBudgetGraph(BudgetCategory category, IList<BudgetTransaction> transactions)
    {
        var balance = category.Limit;
        var output = new GraphData<DateTime, float> ();
        for(int i = 0; i < category.Period.Days; ++i)
        {
            var date = category.Period.StartDate + TimeSpan.FromDays(i);
            var dailyExpense = transactions.Where(x => x.Timestamp.Date == date).Sum(e => e.Cost);
            balance -= dailyExpense;            
            output.Add(date, balance);
        }
        
        return output;
    }
}
public class VisualiseBudgetCommandParser : CommandParser
{   
    public override Command Parse(CommandLineArg[] args)
    {
        if(args.Length == 2 && Endpoints.GetBudgetCategory(args[0]) != null && args[1] == "show")
        {
            return new VisualiseBudgetCommand(args[0]);
        }

        return null;
    }
}
public class ReportBudgetCommandParser : CommandParser
{
    public string[] Keywords = new string[] { "budget", "بوجه" };
    public override Command Parse(CommandLineArg[] args)
    {
        if (args.Length == 1 && args[0].AsString.Is(Keywords))
        {
            return new ReportBudgetCommand();
        }
        else if (args.Length == 1 && args[0].IsAlpha)
        {
            return new ReportBudgetCommand(args[0]);
        }

        return null;
    }
}

public class CommandLineArg
{
    public string AsString;
    public int AsInt;
    public float AsFloat;
    public DateTime AsDate;
    public bool IsInt;
    public bool IsFloat;
    public bool IsNumeric;
    public bool IsAlpha;
    public bool IsDate;

    public static implicit operator string (CommandLineArg arg)
    {
        return arg.AsString;
    }

    public override bool Equals(object obj)
    {
        var otherArg = obj as CommandLineArg;
        if (otherArg != null)
        {
            return string.Compare(AsString, otherArg.AsString, true) == 0;
        }

        return base.Equals(obj);
    }
    public CommandLineArg(string arg)
    {
        AsString = arg;
        IsInt = int.TryParse(arg, out AsInt);
        IsFloat = float.TryParse(arg, out AsFloat);
        IsDate = DateTime.TryParse(arg, out AsDate);
        IsNumeric = IsInt || IsFloat;
        IsAlpha = !IsNumeric && !IsDate;
    }
}

public class DeleteBudgetCommandParser : CommandParser
{
    public string[] Keywords = new string[] { "budget", "بوجه" };
    public override Command Parse(CommandLineArg[] args)
    {
        // delete food 
        if (args.Length == 2 && args[0].AsString.Is("delete") && Endpoints.GetBudgetCategory(args[1]) != null)
        {
            return new DeleteBudgetCommand(args[1]);
        }

        return null;
    }
}

public class AllocateBudgetCommandParser : CommandParser
{
    public string[] Keywords = new string[] { "budget", "بوجه" };
    public override Command Parse(CommandLineArg[] args)
    {
        // budget food 200 20/12 1m
        if (args.Length == 4 && args[0].AsString.Is(Keywords) && args[1].IsAlpha && args[2].IsFloat && args[3].IsDate)
        {
            return new AllocateBudgetCommand(args[1], args[2].AsFloat, args[3].AsDate, BudgetInterval.OneMonth);
        }

        BudgetInterval interval;
        if (args.Length == 5 && args[0].AsString.Is(Keywords) && args[1].IsAlpha && args[2].IsFloat && args[3].IsDate && args[4].IsAlpha && BudgetInterval.TryParse(args[4], out interval))
        {
            return new AllocateBudgetCommand(args[1], args[2].AsFloat, args[3].AsDate, interval);
        }

        return null;
    }
}


public class ClearListCommandParser : CommandParser
{
    public override Command Parse(CommandLineArg[] args)
    {
        if (args.Length == 3 && args[0].IsAlpha && Endpoints.GetBudgetCategory(args[0]) != null && args[1] == "list" && args[2] == "clear")
        {
            return new ClearListCommand(args[0]);
        }

        return null;
    }
}

public class ListCommandParser : CommandParser
{
    public override Command Parse(CommandLineArg[] args)
    {
        if (args.Length == 2 && args[0].IsAlpha && args[1] == "list" && Endpoints.GetBudgetCategory(args[0]) != null)
        {
            return new ListCommand(args[0]);
        }

        return null;
    }
}

public class BudgetEndpoints
{
    ServiceStack.Redis.RedisClient s_redis;

    public BudgetEndpoints(ServiceStack.Redis.RedisClient redis)
    {
        s_redis = redis;
    }

    public void ClearBudgetTransactions(string category)
    {
        var emptyTransactions = new BudgetTransactions();
        s_redis.Set("budget-" + category.ToLower(), emptyTransactions.AsJson());
        s_redis.Save();
    }
    
    public BudgetTransactions FetchBudgetTransactions(string category)
    {
        var transactionJson = s_redis.GetValue("budget-" + category.ToLower());
        var budgetList = BudgetTransactions.FromJson(transactionJson);
        return budgetList;
    }

    public BudgetCategory GetBudgetCategory(string category)
    {
        var budgetList = FetchBudgetList();
        return budgetList[category];
    }

    public float GetBalanceAt(string categoryName, DateTime time)
    {
        var allTransactions = FetchBudgetTransactions(categoryName);
        var category = GetBudgetCategory(categoryName);
        var allExpenses = allTransactions.Where(o => o.Timestamp.CompareTo(category.Period.StartDate) > 0 && o.Timestamp.CompareTo(time) > 0).Sum(x => x.Cost);
        var newBalance = category.Limit - allExpenses;
        return newBalance;
    }

    public float GetBalance(string categoryName)
    {
        var allTransactions = FetchBudgetTransactions(categoryName);
        var category = GetBudgetCategory(categoryName);
        var allExpenses = allTransactions.Where(o => o.Timestamp.CompareTo(category.Period.StartDate) > 0).Sum(x => x.Cost);
        var newBalance = category.Limit - allExpenses;
        return newBalance;
    }

    public BudgetList FetchBudgetList()
    {
        var budgetJson = s_redis.GetValue("budgets");
        var budgetList = BudgetList.ParseJson(budgetJson);
        if (budgetList == null)
        {
            budgetList = new BudgetList();
        }

        return budgetList;
    }

    public BudgetTransaction AddTransaction(string category, float cost, DateTime timestamp, string notes = "")
    {
        var transactions = FetchBudgetTransactions(category);
        var newTransaction = new BudgetTransaction(timestamp, category, cost, notes);
        transactions.Add(newTransaction);

        s_redis.SetValue("budget-" + category.ToLower(), transactions.AsJson());
        s_redis.Save();
        return newTransaction;
    }

    public bool RemoveBudgetCategory(string newCategory)
    {
        var removed = false;
        var budgetList = FetchBudgetList();
        var budgetCategory = budgetList[newCategory];
        if (budgetCategory != null)
        {
            removed = budgetList.Categories.Remove(budgetCategory);
        }
                
        s_redis.SetValue("budgets", budgetList.AsJson());
        s_redis.Save();

        return removed;
    }
    
    public BudgetList AddBudgetCategory(string newCategory, float budget, DateTime startDate, BudgetInterval interval)
    {
        var budgetList = FetchBudgetList();
        var budgetCategory = budgetList[newCategory];
        if (budgetCategory == null)
        {
            budgetCategory = new BudgetCategory(newCategory);
            budgetList.Categories.Add(budgetCategory);
        }

        budgetCategory.Limit = budget;
        budgetCategory.Period = new BudgetPeriod(startDate, interval);

        s_redis.SetValue("budgets", budgetList.AsJson());
        s_redis.Save();
        //s_redis.Set("budgets", budgetList);
        //s_redis.Add("budgets", budgetList);
        return budgetList;
    }
}

[Serializable]
public class BudgetTransaction
{
    public DateTime Timestamp;
    public string Category;
    public float Cost;
    public string Notes;

    public BudgetTransaction()
    {
    }

    public BudgetTransaction(DateTime timestamp, string category, float cost, string notes = "")
    {
        Timestamp = timestamp;
        Category = category;
        Cost = cost;
        Notes = notes;
    }
}

[Serializable]
public class BudgetTransactions : List<BudgetTransaction>
{
    //public List<BudgetTransaction> Transactions = new List<BudgetTransaction>();

    public static BudgetTransactions FromJson(string json)
    {
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<BudgetTransactions>(json);
        }
        catch { }
        return new BudgetTransactions();
    }

    public string AsJson()
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this);
    }
}

public enum BudgetIntervals
{
    Days,
    Monthly,
    Quaterly,
    Yearly,
}

[Serializable]
public class BudgetInterval
{
    public static readonly BudgetInterval OneMonth = new BudgetInterval(BudgetIntervals.Monthly, 1);

    public BudgetIntervals Units = BudgetIntervals.Monthly;
    public int Span = 1;

    public BudgetInterval() { }
    public BudgetInterval(BudgetIntervals units, int span)
    {
        Units = units;
        Span = span;
    }

    public static bool TryParse(string text, out BudgetInterval outInterval)
    {
        outInterval = null;

        if (string.IsNullOrEmpty(text) || string.IsNullOrWhiteSpace(text) || text.Length < 2)
        {
            return false;
        }

        BudgetIntervals interval = BudgetIntervals.Monthly;
        text = text.Trim().ToLower();
        var units = text[text.Length - 1];

        switch (units)
        {
            case 'd':
                interval = BudgetIntervals.Days;
                break;
            case 'm':
                interval = BudgetIntervals.Monthly;
                break;
            case 'q':
                interval = BudgetIntervals.Quaterly;
                break;
            case 'y':
                interval = BudgetIntervals.Yearly;
                break;
        }

        var spanText = text.Substring(0, text.Length - 1);
        int span = 0;
        if (!int.TryParse(spanText, out span))
        {
            return false;
        }

        outInterval = new BudgetInterval(interval, span);
        return true;
    }
}

[Serializable]
public class BudgetPeriod
{
    public int Days { get{ return (End - StartDate).Days; } }

    public DateTime StartDate;
    public BudgetInterval Interval = new BudgetInterval();

    public int RemainingDays
    {
        get
        {
            var remainingDays = (End - DateTime.Now).Days;
            return remainingDays;
        }
    }

    public DateTime End
    {
        get
        {
            switch (Interval.Units)
            {
                case BudgetIntervals.Monthly:
                    return StartDate.AddMonths(Interval.Span);
                case BudgetIntervals.Quaterly:
                    return StartDate.AddMonths(3 * Interval.Span);
                case BudgetIntervals.Days:
                    return StartDate.AddDays(Interval.Span);
            }

            return DateTime.Now;
        }
    }

    public BudgetPeriod()
    {
    }

    public BudgetPeriod(DateTime startDate, BudgetInterval interval)
    {
        StartDate = startDate;
        Interval = interval;
    }
}

[Serializable]
public class BudgetCategory
{
    public string Name;
    public float Limit;
    public BudgetPeriod Period = new BudgetPeriod();

    public BudgetCategory()
    {
    }

    public BudgetCategory(string name)
    {
        Name = name;
    }

    public BudgetCategory(string name, float limit, BudgetPeriod period)
    {
        Name = name;
        Limit = limit;
        Period = period;
    }
}

[Serializable]
public class BudgetList
{
    public BudgetCategory this[string categoryName]
    {
        get { return Categories.Find(x => string.Compare(x.Name, categoryName, true) == 0); }
    }

    public List<BudgetCategory> Categories = new List<BudgetCategory>();

    public static BudgetList ParseJson(string json)
    {
        try
        {
            return Newtonsoft.Json.JsonConvert.DeserializeObject<BudgetList>(json);
        }
        catch { }
        return new BudgetList();
    }

    public string AsJson()
    {
        return Newtonsoft.Json.JsonConvert.SerializeObject(this);
    }
}

public class BudgetBotService
{
    List<CommandParser> s_commandParsers = new List<CommandParser>();
    ServiceStack.Redis.RedisClient s_redis;
    BudgetEndpoints s_endpoints;

    void InterpretCommand(Command command, DateTime timestamp)
    {
        // Just implementing execution from within the command for now.
        command.Endpoints = s_endpoints;
        command.OnMessage += OnCommandMessage;
        command.Execute(timestamp);
    }

    private void OnCommandMessage(string obj)
    {
        Message(obj);
    }

    bool FindCommandParser(string commandString, out Command outCommand)
    {
        commandString = commandString.Trim();
        if (commandString == string.Empty)
        {
            outCommand = null;
            return false;
        }

        commandString = commandString.ToLower();
        var commandArgStrings = commandString.Split(new char[] { ' ' });
        var args = new List<CommandLineArg>(commandArgStrings.Length);
        foreach (var argString in commandArgStrings)
        {
            args.Add(new CommandLineArg(argString));
        }
        args.RemoveAll(x => x == "" || string.IsNullOrWhiteSpace(x.AsString));

        foreach (var parser in s_commandParsers)
        {
            try
            {
                var parsedCommand = parser.Parse(args.ToArray());
                if (parsedCommand != null)
                {
                    outCommand = parsedCommand;
                    return true;
                }
            }
            catch { }
        }

        outCommand = null;
        return false;
    }

    public event Action<string> OnMessage;
    public void Message(string message)
    {
        Console.WriteLine(message);
        OnMessage.SafeInvoke(message);
    }

    public BudgetBotService()
    {
        s_redis = ServiceStack.Redis.RedisClient.New();
        s_endpoints = new BudgetEndpoints(s_redis);

        AddCommandParser(new ListCommandParser());
        AddCommandParser(new ClearListCommandParser());
        AddCommandParser(new AllocateBudgetCommandParser());
        AddCommandParser(new DeleteBudgetCommandParser());        
        AddCommandParser(new AddTransactionCommandParser());
        AddCommandParser(new ReportBudgetCommandParser());
        AddCommandParser(new VisualiseBudgetCommandParser());        
    }
    
    public void ProcessCommand(string userCommand, DateTime timestamp)
    {
        Command command;
        if (FindCommandParser(userCommand, out command))
        {
            //Console.WriteLine("Command found");
            InterpretCommand(command, timestamp);
            Message("");
        }
        else
        {
            Message("Command not found");
        }
    }

    void AddCommandParser(CommandParser parser)
    {
        parser.Endpoints = s_endpoints;
        parser.OnMessage += OnParserMessage;
        s_commandParsers.Add(parser);
    }

    private void OnParserMessage(string message)
    {
        Message(message);
    }
}
//namespace BudgetBotTest
//{

    class Program
    {
        static BudgetBotService Service;
        static void Main(string[] args)
        {
            Service = new BudgetBotService();

            while (true)
            {
                var userCommand = Console.ReadLine();
                Service.ProcessCommand(userCommand, DateTime.Now);
            }
        }       
    }
//}
