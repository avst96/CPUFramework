namespace CPUFramework
{
    public class SQLUtility
    {
        public static string ConnectionString = "";

        public static SqlCommand GetSqlCommand(string sprocname)
        {
            SqlCommand cmd;
            using (SqlConnection conn = new SqlConnection(SQLUtility.ConnectionString))
            {
                cmd = new SqlCommand(sprocname, conn);
                cmd.CommandType = CommandType.StoredProcedure;

                conn.Open();

                SqlCommandBuilder.DeriveParameters(cmd);
            }
            return cmd;
        }
        public static DataTable GetDataTable(SqlCommand cmd)
        {
            return DoExcuteSQL(cmd, true);
        }

        private static DataTable DoExcuteSQL(SqlCommand cmd, bool loadtable)
        {
            DataTable dt = new();
            using (SqlConnection conn = new SqlConnection(SQLUtility.ConnectionString))
            {
                conn.Open();
                cmd.Connection = conn;
                Debug.Print(GetSQL(cmd));
                try
                {
                    SqlDataReader dr = cmd.ExecuteReader();
                    CheckReturnValue(cmd);
                    if (loadtable)
                    {
                        dt.Load(dr);
                        SetAllColumnsProperties(dt);
                    }
                }
                catch (SqlException ex)
                {
                    string msg = ParseConstraintMsg(ex.Message);
                    throw new Exception(msg);
                }
                catch (InvalidCastException ex)
                {
                    throw new Exception(cmd.CommandText + ex.Message, ex);
                }
            }
            return dt;
        }
        public static int GetNewPrimaryKey(SqlCommand cmd, string primarykeyname)
        {
            int primarykey = 0;
            primarykeyname = primarykeyname.ToLower();
            if(cmd.CommandType == CommandType.StoredProcedure)
            {
                foreach(SqlParameter p in cmd.Parameters)
                {
                    if(p.Direction == ParameterDirection.InputOutput && p.ParameterName.ToLower() == $"@{primarykeyname}" && p.Value is int i)
                    {
                        primarykey = i;
                    }
                }
            }
            return primarykey;   
        } 
        private static void CheckReturnValue(SqlCommand cmd)
        {
            int returnvalue = 0;
            string msg = "";
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                foreach (SqlParameter p in cmd.Parameters)
                {
                    if (p.Direction == ParameterDirection.ReturnValue && p.Value != null)
                    {
                        returnvalue = (int)p.Value;
                    }
                    else if (p.ParameterName.ToLower() == "@message" && p.Value != null)
                    {
                        msg = p.Value.ToString();
                    }
                }
                if (returnvalue == 1)
                {
                    if (msg == "")
                    {
                        msg = $"{cmd.CommandText} did not do action requested";
                    }
                    throw new Exception(msg);
                }
            }

        }
        public static DataTable GetDataTable(string sqlstatement)
        {
            return DoExcuteSQL(new SqlCommand(sqlstatement), true);
        }

        public static void ExecuteSQL(string sqlstatement)
        {
            GetDataTable(sqlstatement);
        }
        public static void SaveDataTable(DataTable dt, string sprocname)
        {
            var rows = dt.Select("", "", DataViewRowState.Added | DataViewRowState.ModifiedCurrent);
            foreach (DataRow r in rows)
            {
                SaveDataRow(r, sprocname, false);
            }
            dt.AcceptChanges();
        }

        public static void SaveDataRow(DataRow row, string sprocname, bool acceptchanges = true)
        {
            SqlCommand cmd = GetSqlCommand(sprocname);
            foreach (DataColumn col in row.Table.Columns)
            {
                string paramname = $"@{col.ColumnName}";
                if (cmd.Parameters.Contains(paramname))
                {
                    cmd.Parameters[paramname].Value = row[col.ColumnName];
                }
            }
            DoExcuteSQL(cmd, false);

            foreach (SqlParameter p in cmd.Parameters)
            {
                if (p.Direction == ParameterDirection.InputOutput)
                {
                    string colname = p.ParameterName.Substring(1);
                    if (row.Table.Columns.Contains(colname))
                    {
                        row[colname] = p.Value;
                    }
                }
            }

            if (acceptchanges)
            {
                row.Table.AcceptChanges();
            }
        }

        public static void ExecuteSQL(SqlCommand cmd)
        {
            DoExcuteSQL(cmd, false);
        }

        public static void SetParamValue(SqlCommand cmd, string paramname, object value)
        {
            try
            {
                cmd.Parameters[paramname].Value = value;
            }
            catch (Exception ex)
            {
                throw new Exception(cmd.CommandText + ": " + ex.Message, ex);
            }
        }

        public static string ParseConstraintMsg(string msg)
        {
            string origmsg = msg;
            string prefix = "ck_";
            string msgend = "";
            string notnullprefix = "Cannot insert the value NULL into column '";
            if (msg.Contains(prefix) == false)
            {
                if (msg.Contains("u_"))
                {
                    prefix = "u_";
                    msgend = " must be unique";
                }
                else if (msg.Contains("f_"))
                {
                    prefix = "f_";
                }
                else if (msg.Contains(notnullprefix))
                {
                    prefix = notnullprefix;
                    msgend = " cannot be blank.";
                }
            }
            if (msg.Contains(prefix))
            {
                int pos = msg.IndexOf(prefix) + prefix.Length;
                msg = msg.Replace("\"", "'");
                msg = msg.Substring(pos);
                pos = msg.IndexOf("\'");
                if (pos < 0)
                {
                    msg = origmsg;
                }
                else
                {
                    msg = msg.Substring(0, pos);
                    msg = msg.Replace("_", " ");
                    msg += msgend;
                    msg = char.ToUpper(msg[0]) + msg.Substring(1);

                    if (prefix == "f_")
                    {
                        var words = msg.Split(' ');
                        if (words.Length > 1)
                        {
                            msg = $"Cannot delete {words[0]} because it has a related {words[1]} record";
                        }
                    }
                }
            }
            return msg;
        }
        public static int GetFirstColumnFirstRowValue(string sql)
        {
            int n = 0;

            DataTable dt = GetDataTable(sql);
            if (dt.Rows.Count > 0 && dt.Columns.Count > 0)
            {
                if (dt.Rows[0][0] != DBNull.Value)
                {
                    int.TryParse(dt.Rows[0][0].ToString(), out n);
                }
            }
            return n;
        }

        private static void SetAllColumnsProperties(DataTable dt)
        {
            foreach (DataColumn c in dt.Columns)
            {
                c.AllowDBNull = true;
                c.AutoIncrement = false;
            }
        }

        public static int GetValueFromFirstRowAsInt(DataTable dt, string columnname)
        {
            int value = 0;
            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                if (r[columnname] != null && r[columnname] is int)
                {
                    value = (int)r[columnname];
                }
            }
            return value;
        }

        public static string GetValueFromFirstRowAsString(DataTable dt, string columnname)
        {
            string value = "";
            if (dt.Rows.Count > 0)
            {
                DataRow r = dt.Rows[0];
                if (r[columnname] != null && r[columnname] is string)
                {
                    value = (string)r[columnname];
                }
            }
            return value;
        }

        public static bool TableHasChanges(DataTable dt)
        {
            bool b = false;
            if (dt.GetChanges() != null)
            {
                b = true;
            }
            return b;
        }

        public static string GetSQL(SqlCommand cmd)
        {
            string val = "";
#if DEBUG
            StringBuilder sb = new();
            if (cmd.Connection != null)
            {
                sb.AppendLine($"--{cmd.Connection.DataSource}");
                sb.AppendLine($"use {cmd.Connection.Database}{Environment.NewLine}go");

            }
            if (cmd.CommandType == CommandType.StoredProcedure)
            {
                int paramcount = cmd.Parameters.Count - 1;
                int paramnum = 0;
                string comma = ",";
                sb.AppendLine($"exec {cmd.CommandText}");

                foreach (SqlParameter p in cmd.Parameters)
                {
                    if (p.Direction != ParameterDirection.ReturnValue)
                    {
                        if (paramnum == paramcount)
                        {
                            comma = "";
                        }
                        sb.AppendLine($"{p.ParameterName} = {(p.Value == null ? "default" : p.Value.ToString())}{comma}");
                    }
                    paramnum++;
                }
            }
            else
            {
                sb.AppendLine(cmd.CommandText);
            }
            val = sb.ToString();
#endif
            return val;
        }

        public static void DebugPrintDataTable(DataTable dt)
        {
#if DEBUG
            Debug.Print("-----------------------");
            foreach (DataRow r in dt.Rows)
            {
                foreach (DataColumn c in dt.Columns)
                {
                    Debug.Print(c.ColumnName + " = " + r[c.ColumnName].ToString());
                }
            }
#endif
        }
    }
}
