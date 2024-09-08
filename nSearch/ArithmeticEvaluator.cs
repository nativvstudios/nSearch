using System.Data;

public static class ArithmeticEvaluator
{
    public static string EvaluateArithmeticExpression(string query)
    {
        try
        {
           DataTable dt = new DataTable();
           var result = dt.Compute(query, "");
           return result.ToString();
        }
        catch
        {
           return null;
        }
    }
}