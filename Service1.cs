using System;
using System.Configuration;
using System.Data.SqlClient;
using System.IO;
using System.ServiceProcess;
using System.Threading.Tasks;

namespace ServicesZabbix
{
    public partial class Service1 : ServiceBase
    {
        public Service1()
        {
            InitializeComponent();
        }

        protected override async void OnStart(string[] args)
        {
            //Взятие данных из AppConfig
            string server = ConfigurationManager.AppSettings["Server"];
            string database = ConfigurationManager.AppSettings["Database"];
            string userId = ConfigurationManager.AppSettings["User_ID"];
            string password = ConfigurationManager.AppSettings["Password"];
            string path = ConfigurationManager.AppSettings["PathToScript"];
            int timeUpdate = int.Parse(ConfigurationManager.AppSettings["TimeUpdate"]);

            //Проверка на сущестоввание директории
            if (!Directory.Exists($"{path}"))
                Directory.CreateDirectory($"{path}");

            //Строка подключения к серверу MSSQL
            string connectionString = $"Server={server};Database={database};User ID={userId};Password={password};Encrypt=false";

            //Запрос на выборку последнего снимка в экспорте
            string SQLQuery = "SELECT MAX(CHECKTIME) AS MAX_CHECKTIME ,CHANNELS.CHANNEL_ID, CHANNELS.NAME, UNITS.UNIT_ID,  UNITLOCATIONS.SETUPPLACE " +
                "FROM CARS " +
                "INNER JOIN CHANNELS ON CARS.CHANNEL_ID = CHANNELS.ID " +
                "INNER JOIN UNITS ON CARS.UNIT_ID = UNITS.ID " +
                "INNER JOIN UNITLOCATIONS ON UNITS.UNIT_ID = UNITLOCATIONS.UNIT_ID " +
                "GROUP BY CHANNELS.CHANNEL_ID, UNITS.UNIT_ID, UNITLOCATIONS.SETUPPLACE, CHANNELS.NAME " +
                "ORDER BY MAX(CHECKTIME) DESC";

            while (true)
            {
                //Попытка подключения к SQL
                using (SqlConnection connection = new SqlConnection(connectionString))
                {
                    await connection.OpenAsync();

                    //Подключение запроса SQL и чтения запроса
                    SqlCommand command = new SqlCommand(SQLQuery, connection);
                    SqlDataReader reader = await command.ExecuteReaderAsync();

                    if (reader.HasRows) //Если есть данные
                    {
                        //Выводим названия столбцов
                        string column1 = reader.GetName(0);
                        string column2 = reader.GetName(1);
                        string column3 = reader.GetName(2);
                        string column4 = reader.GetName(3);
                        string column5 = reader.GetName(4);

                        File.WriteAllText($@"{path}\log.txt", $"{column1}\t{column2}\t{column3}\t{column4}\t{column5}\n");
                    }
                    while (await reader.ReadAsync()) //Построчно считываем данные
                    {
                        DateTime now = DateTime.Now;
                        DateTime checktime = Convert.ToDateTime(reader.GetValue(0)).AddHours(6);
                        object channel_id = reader.GetValue(1);
                        object name = reader.GetValue(2);
                        object unit_id = reader.GetValue(3);
                        object unitLocatoins = reader.GetValue(4);

                        TimeSpan diff = now - checktime; //Разница между последним сформированным снимком и нашим временем

                        if (diff.TotalMinutes > 30) //Если снимков нет больше 30 минут
                        {
                            if (!Directory.Exists($"{path}"))
                                Directory.CreateDirectory($"{path}");

                            //Пишем в файл время последней записи
                            File.WriteAllText($@"{path}\{name}.txt", checktime.ToString());
                        }
                        else
                        {
                            if (!Directory.Exists($"{path}"))
                                Directory.CreateDirectory($"{path}");

                            //В противном случае пишем что всё ОК
                            File.WriteAllText($@"{path}\{name}.txt", "Ok");
                        }

                        File.AppendAllText($@"{path}\log.txt", $"{checktime} \t{channel_id} \t{name} \t{unit_id} \t{unitLocatoins} \t{diff.TotalMinutes}\n");

                    }
                    await Task.Delay(timeUpdate);
                }
            }
        }

    }
}
