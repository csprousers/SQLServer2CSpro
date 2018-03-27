using CSPro.Dictionary;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLServer2Dictionary
{
    /// <summary>
    /// Outil pour avoir le value set (les reponses) des questions
    /// a partir de la base SQL
    /// </summary>
    class ValueSetRetriever : IDisposable
    {
        private SqlConnection connection;
        private string queryTemplate;

        /// <summary>
        /// Creer le ValueSetRetriever et se connecte a la base
        /// </summary>
        /// <param name="connectionString">Connection string pour la base SQL</param>
        /// <param name="queryTemplate">Requete pour avoir le value set a partir du libelle du item</param>
        public ValueSetRetriever(string connectionString, string queryTemplate)
        {
            connection = new SqlConnection(connectionString);
            connection.Open();
            this.queryTemplate = queryTemplate;
        }

        public void Dispose()
        {
            connection.Dispose();
        }

        /// <summary>
        /// Chercher les reponses pour la question de la base SQL et les ajouter au value set pour item.
        /// </summary>
        /// <param name="item">Variable du dictionnaire CSPro</param>
        public void GetValueSet(DictionaryItem item)
        {
            try
            {
                if (String.IsNullOrEmpty(queryTemplate))
                    return;

                string query = queryTemplate.Replace("%item%", item.Label);

                var cmd = new SqlCommand(query, connection);

                using (var reader = cmd.ExecuteReader())
                {
                    if (reader.HasRows)
                    {
                        var valueSet = item.AddValueSet();
                        valueSet.Name = item.Name;
                        int maxNameLen = 32;
                        string valuesetPost = "_VS1";
                        if (valueSet.Name.Length > maxNameLen - valuesetPost.Length)
                            valueSet.Name = valueSet.Name.Substring(0, maxNameLen - valuesetPost.Length);
                        valueSet.Name += valuesetPost;
                        valueSet.Label = item.Label;
                        while (reader.Read())
                        {
                            string codeReponse = reader.GetString(0);
                            string libelleReponse = reader.GetString(1);

                            var value = valueSet.AddValue();
                            value.Label = libelleReponse;
                            value.AddValuePair(String.Format("{0," + item.Length + "}", codeReponse));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception("Invalid value set query: " + ex.Message, ex.InnerException);
            }
        }
    }
}
