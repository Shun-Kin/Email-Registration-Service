using System;
using System.Data;
using System.Data.SqlClient;
using System.Collections.Generic;


namespace Email_Registration_Service
{
    // Перечисление операций над БД
    public enum EmailOperation: byte { Add, Update, GetAll, GetById, GetByPeriod, GetByTo, GetByFrom, GetByTag }


    public class EmailRegistration : IEmailRegistration
    {
        // Строка подключения к БД
        static string connectionString = @"Data Source=localhost\SQLEXPRESS;Initial Catalog=emailsdb;Integrated Security=True";

        // Соотношение операций и названий хранимых процедур БД
        static private Dictionary<EmailOperation, string> procedureNames = new Dictionary<EmailOperation, string>
        {
            [EmailOperation.Add]         = "sp_AddEmail",
            [EmailOperation.Update]      = "sp_UpdateEmail",
            [EmailOperation.GetAll]      = "sp_GetEmails",
            [EmailOperation.GetById]     = "sp_GetEmailById",
            [EmailOperation.GetByPeriod] = "sp_GetEmailByPeriod",
            [EmailOperation.GetByTo]     = "sp_GetEmailByTo",
            [EmailOperation.GetByFrom]   = "sp_GetEmailByFrom",
            [EmailOperation.GetByTag]    = "sp_GetEmailByTag",
        };


        public bool TestConnection()
        {
            return true;
        }


        public void AddEmail(EmailFull email)
        {
            EmailExecute(EmailOperation.Add, email);
        }


        public void UpdateEmail(EmailFull email, string[] unrelatedTags)
        {
            EmailExecute(EmailOperation.Update, email, unrelatedTags);
        }


        public Email[] GetEmails()
        {
            return GetEmails(EmailOperation.GetAll);
        }


        public Email[] GetEmailsByPeriod(DateTime _date_left, DateTime _date_right)
        {
            return GetEmails(EmailOperation.GetByPeriod, date_left: _date_left, date_right: _date_right);
        }


        public Email[] GetEmailsByTo(string _address)
        {
            return GetEmails(EmailOperation.GetByTo, address: _address);
        }


        public Email[] GetEmailsByFrom(string _address)
        {
            return GetEmails(EmailOperation.GetByFrom, address: _address);
        }


        public Email[] GetEmailsByTag(string _tag)
        {
            return GetEmails(EmailOperation.GetByTag, tag: _tag);
        }


        public EmailFull GetEmailById(int _id)
        {
            EmailFull email = null;

            // Создание подключения
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();
                // Создание команды для выполнения к БД
                SqlCommand command = new SqlCommand(procedureNames[EmailOperation.GetById], connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                // Добавление входного параметра хранимой процедуры
                SqlParameter id_Email = command.Parameters.Add("@id_Email", SqlDbType.Int);
                id_Email.Value = _id;

                // Выполнение команды и получение объекта чтения данных
                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    // Чтение строки данных
                    reader.Read();
                    email = new EmailFull
                    {
                              id = _id,
                         subject = reader.GetString(0),
                        date_reg = reader.GetDateTime(1),
                              to = reader.GetString(2),
                            from = reader.GetString(3),
                            text = reader.GetString(4)
                    };
                    reader.Close();

                    // Запрос тегов
                    command.CommandText = "sp_GetTagsByEmailId";

                    reader = command.ExecuteReader();
                    if (reader.HasRows)
                    {
                        List<string> tags = new List<string>();

                        while (reader.Read())
                        {
                            tags.Add(reader.GetString(0));
                        }

                        email.tags = tags.ToArray();
                    }
                    reader.Close();
                }

                return email;
            }
        }


        // Общий метод добавления и обновления писем
        private void EmailExecute(EmailOperation operation, EmailFull email, string[] unrelatedTags = null)
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand("sp_AddUserAdress", connection) {
                    CommandType = CommandType.StoredProcedure
                };

                // Транзакция для безопасности
                SqlTransaction transaction = connection.BeginTransaction();
                command.Transaction = transaction;

                try
                {
                    // Добавление пользователей
                    SqlParameter address = command.Parameters.Add("@address", SqlDbType.NVarChar);
                    address.Value = email.to;

                    // Параметр для результата (id письма)
                    SqlParameter id = command.Parameters.Add("@Return", SqlDbType.Int);
                    id.Direction = ParameterDirection.ReturnValue;

                    command.ExecuteNonQuery();
                    // id адресата
                    int user_to_id = (int)id.Value;

                    address.Value = email.from;
                    command.ExecuteNonQuery();
                    // id отправителя
                    int user_from_id = (int)id.Value;


                    // Добавление или обновление письма
                    command.CommandText = procedureNames[operation];

                    // Параметры
                    command.Parameters.Remove(address);

                    SqlParameter subject = command.Parameters.Add("@subject", SqlDbType.NVarChar);
                    subject.Value = email.subject;

                    SqlParameter id_UserTo = command.Parameters.Add("@id_UserTo", SqlDbType.Int);
                    id_UserTo.Value = user_to_id;

                    SqlParameter id_UserFrom = command.Parameters.Add("@id_UserFrom", SqlDbType.Int);
                    id_UserFrom.Value = user_from_id;

                    SqlParameter text = command.Parameters.Add("@text", SqlDbType.NText);
                    text.Value = email.text;

                    // Операция добавления или обновления
                    if (operation == EmailOperation.Add)
                    {
                        SqlParameter date_reg = command.Parameters.Add("@date_reg", SqlDbType.DateTime);
                        date_reg.Value = email.date_reg;
                    }
                    else
                    {   // Переиспользование параметра (теперь он - входной)
                        id.ParameterName = "@id";
                        id.Direction = ParameterDirection.Input;
                        id.Value = email.id;
                    }

                    command.ExecuteNonQuery();

                    // Удаление тегов из письма
                    if (operation == EmailOperation.Update)
                    {
                        TagsExecute("sp_DeleteTagRelation", command, unrelatedTags, email.id);
                    }

                    // Добавление тегов
                    TagsExecute("sp_AddTag", command, email.tags, (int)id.Value);

                    // Подтверждение транзакции
                    transaction.Commit();
                }
                catch (Exception)
                {
                    // Откат транзакции
                    transaction.Rollback();
                    throw;
                }
            }
        }


        // Добавление или удаление тегов
        private void TagsExecute(string commandText, SqlCommand command, string[] tagArray, int id_email)
        {
            if (tagArray != null)
            {
                command.CommandText = commandText;
                command.Parameters.Clear();

                SqlParameter _id_email = command.Parameters.Add("@id_email", SqlDbType.Int);
                _id_email.Value = id_email;

                SqlParameter name = command.Parameters.Add("@name", SqlDbType.NVarChar);

                int tagsCount = tagArray.Length;
                for (int i = 0; i < tagsCount; i++)
                {
                    name.Value = tagArray[i];
                    command.ExecuteNonQuery();
                }
            }
        }


        // Общий метод получения писем
        private Email[] GetEmails(EmailOperation operation, DateTime date_left = default, DateTime date_right = default, string address = "", string tag = "")
        {
            using (SqlConnection connection = new SqlConnection(connectionString))
            {
                connection.Open();

                SqlCommand command = new SqlCommand(procedureNames[operation], connection)
                {
                    CommandType = CommandType.StoredProcedure
                };

                // Установка входных параметров в зависимости от критерия поиска
                switch (operation)
                {
                    case EmailOperation.GetByPeriod:
                        SqlParameter _date_left = command.Parameters.Add("@date_left", SqlDbType.DateTime);
                        _date_left.Value = date_left;

                        SqlParameter _date_right = command.Parameters.Add("@date_right", SqlDbType.DateTime);
                        _date_right.Value = date_right;
                        break;

                    case EmailOperation.GetByTo:
                    case EmailOperation.GetByFrom:
                        SqlParameter _address = command.Parameters.Add("@address", SqlDbType.NVarChar);
                        _address.Value = address;
                        break;

                    case EmailOperation.GetByTag:
                        SqlParameter _tag = command.Parameters.Add("@tag", SqlDbType.NVarChar);
                        _tag.Value = tag;
                        break;
                }

                Email[] emailArray = null;

                SqlDataReader reader = command.ExecuteReader();
                if (reader.HasRows)
                {
                    List<Email> emails = new List<Email>();

                    while (reader.Read())
                    {
                        emails.Add(new Email {
                                  id = reader.GetInt32(0),
                             subject = reader.GetString(1),
                            date_reg = reader.GetDateTime(2),
                                  to = reader.GetString(3),
                                from = reader.GetString(4)
                        });
                    }

                    emailArray = emails.ToArray();
                }

                reader.Close();
                return emailArray;
            }
        }
    }
}
