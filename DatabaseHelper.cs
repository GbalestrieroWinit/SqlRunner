using MySqlConnector;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SqlRunner
{
    internal class DatabaseHelper
    {
        public static Dictionary<String, Object?> ExecuteQuery(string query, string connectionString)
        {
            //Oggetto per ritornare il risultato, contiene una struttura con chiave "tabella" e chiave "feedback", tabella è nulla se viene eseguito un comndo che non sia SELECT
            Dictionary <String, Object?> result = new Dictionary <String, Object?> ();
            String feedback = "";
            result.Add("tabella", null);


            try
            {
                //connessione a db in uso
                using var connection = new MySqlConnection(connectionString);
                connection.Open();
                using var command = new MySqlCommand(query, connection);
                using var reader = command.ExecuteReader(); //esecuzione del comando

                //FieldCount è > 0 se il comando eseguito restituisce una tabella, quindi una SELECT
                if (reader.FieldCount > 0)
                {
                    //Conversione in Tabella del risultato ottenuto
                    var table = new DataTable();
                    table.Load(reader);
                    result["tabella"] = table;
                    feedback = $"Query eseguita. Righe influenzate: {table.Rows.Count}.";
                }
                else
                {
                    //comando normale e non SELECT
                    feedback = $"Comando eseguito. Righe influenzate: {reader.RecordsAffected}.";
                }
            }
            catch (MySqlException ex)
            {
                feedback = FormatMySqlError(ex);
            }
            catch (Exception ex)
            {
                feedback = ex.Message;
            }

            //compilazione del campo "feedback" del dictionary
            result.Add("feedback", feedback);
            return result;
        }

        /// <summary>
        /// Metodo per la gestione delle eccezioni di tipo Sql
        /// </summary>
        /// <param name="ex">Eccezione da gestire</param>
        /// <returns>Stringa formattata per la stampa</returns>
        private static string FormatMySqlError(MySqlException ex)
        {
            var lines = new List<string> { "Errore SQL:" };
            lines.Add($"- {ex.Message}");
            lines.Add($"  Codice errore: {ex.ErrorCode}; numero: {ex.Number}; stato SQL: {ex.SqlState}");

            return string.Join(Environment.NewLine, lines);
        }
    }
}
