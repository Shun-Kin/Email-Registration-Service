using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Diagnostics;

using ERS_Client_Console.RemoteService;


namespace ERS_Client_Console
{
    class Program
    {
        // Импорт функции отправки сообщения окну
        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern int SendMessage(IntPtr hWnd, int uMsg, int wParam, string lParam);


        // Формат строки для вывода атрибутов письма
        private static readonly string COLUMN_FORMAT = " {0, 6} | {1, -25} | {2, -24} | {3, -25} | {4, -25}";
        private static readonly string[] COLUMN_TITLES = { "ID", "Название", "Дата регистрации", "Адресат", "Отправитель" };
        // Формат строки для вывода атрибутов нового письма
        private static readonly string COLUMN_FORMAT_REG = " {0, -25} | {1, -25} | {2, -25}";
        private static readonly string[] COLUMN_TITLES_REG = { "Название", "Адресат", "Отправитель" };
        private static readonly string DEFAULT_SUBJECT = "Без названия";


        private static string lineString = new string('-', 118);
        // Клиент сервиса
        private static EmailRegistrationClient client;
        // Дескриптор окна консоли
        private static IntPtr consoleWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
        private static EmailFull email = new EmailFull() { subject = "", from = "", to = "", text = "" };
        private static HashSet<string> oldTags = new HashSet<string>();
        private static HashSet<string> tags = new HashSet<string>();


        const int WM_CHAR = 0X0102;


        static void Main(string[] args)
        {
            // Созданиеа клиент сервиса
            client = new EmailRegistrationClient("BasicHttpBinding_IEmailRegistration");

            try
            {
                Console.Write("Проверка соединения с сервисом... ");
                client.TestConnection();
                Console.WriteLine("OK");

                ConsoleKeyInfo key;
                do
                {
                    Console.WriteLine("\n  1 - Получить список писем\n  2 - Получить данные письма по ID\n  3 - Регистрация письма\nEsc - Выйти\n");
                    // параметр true - не отображать ввод
                    key = Console.ReadKey(true);
                    Console.WriteLine("\n");

                    switch (key.Key)
                    {
                        // Получить список писем
                        case ConsoleKey.D1:
                            ShowAllEmails();
                            break;

                        // Получить данные письма по ID
                        case ConsoleKey.D2:
                            Console.Write("Введите ID письма: ");

                            int id_Email;
                            if (int.TryParse(Console.ReadLine(), out id_Email))
                            {
                                ShowEmail(id_Email);
                            }
                            else
                            {
                                Console.WriteLine("Некорректный ID!");
                            }
                            break;

                        // Регистрация письма
                        case ConsoleKey.D3:
                            EmailEditing(COLUMN_FORMAT_REG, COLUMN_TITLES_REG);
                            break;
                    }
                }
                while (key.Key != ConsoleKey.Escape);

                // Закрыть клиент в коннце работы
                client.Close();
            }
            catch (Exception ex)
            {
                // Принудительно закрыть клиент
                client.Abort();
                // Вывод информации об ошибке
                Console.WriteLine($"\nКлиент закрыт.\n\nОшибка:\n{ex.Message}");
                Console.ReadKey();
            }
        }


        static void ShowAllEmails()
        {
            Email[] emailArray = client.GetEmails();

            if (emailArray != null)
            {
                int length = emailArray.Length;
                Console.WriteLine($"Всего писем: {length}\n\n{COLUMN_FORMAT}\n{lineString}", "ID", "Название", "Дата регистрации", "Адресат", "Отправитель");

                for (int i = 0; i < length; i++)
                {
                    Console.WriteLine(COLUMN_FORMAT, emailArray[i].id, emailArray[i].subject, emailArray[i].date_reg, emailArray[i].to, emailArray[i].from);
                }
            }
            else
            {
                Console.WriteLine("Писем нет.");
            }
        }


        static void ShowEmail(int id)
        {
            email = client.GetEmailById(id);
            
            if (email != null)
            {
                if (email.tags != null)
                {
                    for (int i = 0; i < email.tags.Length; i++)
                    {
                        oldTags.Add(email.tags[i]);
                        tags.Add(email.tags[i]);
                    }
                    
                    email.tags = null;
                }

                // Изменение атрибутов письма
                EmailEditing(COLUMN_FORMAT, COLUMN_TITLES, false);
            }
            else
            {
                email = new EmailFull() { subject = "", from = "", to = "", text = "" };
                Console.WriteLine("Письма с таким ID нет.");
            }
        }


        static void EmailEditing(string columnFormat, string[] columnTitles, bool registration = true)
        {
            ConsoleKeyInfo key;
            do
            {
                Console.WriteLine($"\n{columnFormat}\n{lineString}", columnTitles);

                if (registration)
                {
                    Console.WriteLine(columnFormat, email.subject, email.to, email.from);
                }
                else
                {
                    Console.WriteLine(columnFormat, email.id, email.subject, email.date_reg, email.to, email.from);
                }

                Console.WriteLine($"{lineString}\nСодержание:\n\n{email.text}\n{lineString}");
                Console.WriteLine(tags.Count > 0 ? $"Теги:\t#{string.Join("\t#", tags)}" : "Тегов нет");

                Console.WriteLine($"\n  1 - Изменить название\n  2 - Изменить адресата\n  3 - Изменить отправителя\n  4 - Изменить текст\n  5 - Добавить тег\n  6 - Удалить тег\n  0 - {(registration ? "Зарегистрировать" : "Сохранить изменения")}\nEsc - Отмена\n\n");
                key = Console.ReadKey(true);

                switch (key.Key)
                {
                    // Изменить название
                    case ConsoleKey.D1:
                        Console.Write("Введите название: ");
                        ConsoleAutocomplete(email.subject);
                        email.subject = Console.ReadLine();
                        break;

                    // Изменить адресата
                    case ConsoleKey.D2:
                        Console.Write("Введите адресата: ");
                        ConsoleAutocomplete(email.to);
                        email.to = Console.ReadLine();
                        break;

                    // Изменить отправителя
                    case ConsoleKey.D3:
                        Console.Write("Введите отправителя: ");
                        ConsoleAutocomplete(email.from);
                        email.from = Console.ReadLine();
                        break;

                    // Изменить текст
                    case ConsoleKey.D4:
                        Console.Write("Введите текст: ");
                        ConsoleAutocomplete(email.text);
                        email.text = Console.ReadLine();
                        break;

                    // Добавить тег
                    case ConsoleKey.D5:
                        Console.Write("Введите тег: ");

                        string newTag = Console.ReadLine();
                        if (newTag != "")
                        {
                            tags.Add(newTag);
                        }
                        break;

                    // Удалить тег
                    case ConsoleKey.D6:
                        Console.Write("Введите тег для удаления: ");
                        tags.Remove(Console.ReadLine());
                        break;

                    // Сохранить изменения или Зарегистрировать
                    case ConsoleKey.D0:
                        if (email.to != "" && email.from != "")
                        {
                            if (email.subject == "")
                            {
                                email.subject = DEFAULT_SUBJECT;
                            }

                            // Удалённые теги
                            string[] unrelatedTags = null;

                            if (!registration)
                            {
                                HashSet<string> oldTagsTemp = new HashSet<string>(oldTags);
                                oldTags.ExceptWith(tags);
                                if (oldTags.Count > 0)
                                {
                                    unrelatedTags = new string[oldTags.Count];
                                    oldTags.CopyTo(unrelatedTags);
                                }

                                tags.ExceptWith(oldTagsTemp);
                            }

                            // Новые теги
                            if (tags.Count > 0)
                            {
                                email.tags = new string[tags.Count];
                                tags.CopyTo(email.tags);
                            }

                            if (registration)
                            {
                                email.date_reg = DateTime.Now;

                                client.AddEmail(email);
                                Console.WriteLine("Письмо добавлено");
                            }
                            else
                            {
                                client.UpdateEmail(email, unrelatedTags);
                                Console.WriteLine("Изменения сохранены.");
                            }

                            goto exit;
                        }
                        else
                        {
                            Console.WriteLine("Отправитель и Адресат не должны быть пустыми!");
                        }
                        break;

                }
            }
            while (key.Key != ConsoleKey.Escape);

            exit:;

            // Пустое письмо
            email = new EmailFull() { subject = "", from = "", to = "", text = "" };
            oldTags.Clear();
            tags.Clear();
        }


        // Автозаполнение ввода
        static void ConsoleAutocomplete(string str)
        {
            int length = str.Length;
            char curChar = '\0';

            for (int i = 0; i < length; i++)
            {
                if (curChar == str[i])
                {
                    SendMessage(consoleWindowHandle, WM_CHAR, '\0', null);
                }
                curChar = str[i];
                SendMessage(consoleWindowHandle, WM_CHAR, curChar, null);
            }
        }
    }
}
