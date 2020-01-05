using System;
using System.Runtime.Serialization;
using System.ServiceModel;


namespace Email_Registration_Service
{
    [ServiceContract]
    public interface IEmailRegistration
    {
        // Проверка соединения с сервисом
        [OperationContract]
        bool TestConnection();


        // Получение писем
        [OperationContract]
        Email[] GetEmails();

        // Получение письма по его id
        [OperationContract]
        EmailFull GetEmailById(int _id);

        // Добавление письма
        [OperationContract]
        void AddEmail(EmailFull email);

        // Обновление атрибутов письма
        [OperationContract]
        void UpdateEmail(EmailFull email, string[] unrelatedTags);


        // Дополнительные методы
        // Получение писем  за период
        [OperationContract]
        Email[] GetEmailsByPeriod(DateTime date_left, DateTime date_right);

        // Получение писем адресату
        [OperationContract]
        Email[] GetEmailsByTo(string address);

        // Получение писем по отправителю
        [OperationContract]
        Email[] GetEmailsByFrom(string address);

        // Получение писем по тегу
        [OperationContract]
        Email[] GetEmailsByTag(string tag);
    }


    // Класс письма (упрощённый)
    [DataContract]
    public class Email
    {
        [DataMember]
        public int id { get; set; }             // id (индификатор)

        [DataMember]
        public string subject { get; set; }     // Название

        [DataMember]
        public DateTime date_reg { get; set; }  // Дата регистрации

        [DataMember]
        public string to { get; set; }          // Адресат

        [DataMember]
        public string from { get; set; }        // Отправитель
    }


    // Класс письма (полный)
    [DataContract]
    public class EmailFull: Email
    {
        [DataMember]
        public string text { get; set; }    // Содержание (текст)

        [DataMember]
        public string[] tags { get; set; }  // Теги
    }
}
