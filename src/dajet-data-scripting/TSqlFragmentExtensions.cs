using Microsoft.SqlServer.TransactSql.ScriptDom;
using System.Text;

namespace DaJet.Data.Scripting
{
    public static class TSqlFragmentExtensions
    {
        public static string ToSqlString(this TSqlFragment fragment)
        {
            SqlScriptGenerator generator = new Sql150ScriptGenerator();

            generator.GenerateScript(fragment, out string sql);

            return sql;
        }
        public static string ToSourceSqlString(this TSqlFragment fragment)
        {
            StringBuilder sql = new StringBuilder();

            for (int i = fragment.FirstTokenIndex; i <= fragment.LastTokenIndex; i++)
            {
                sql.Append(fragment.ScriptTokenStream[i].Text);
            }

            return sql.ToString();
        }
    }
}