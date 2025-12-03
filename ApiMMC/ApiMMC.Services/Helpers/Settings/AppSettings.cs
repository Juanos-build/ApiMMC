namespace ApiMMC.Services.Helpers.Settings
{
    public class AppSettings
    {
        private static AppSettings setting;
        public static AppSettings Setting { get => setting; set => setting = value; }
        public string Connection { get; set; }
        public int ResponseResult { get; set; }
        public int ErrorResult { get; set; }
        public IntegrationSettings Integration { set; get; }
        public NotificationSettings NotificationSettings { set; get; }
        public FileSettings FileSettings { set; get; }
        public XMSettings XmSettings { set; get; }
    }

    public class NotificationSettings
    {
        public string ScheduledTimeLectura { get; set; }
        public string ScheduledTimeConsulta { get; set; }
    }

    public class FileSettings
    {
        public string Directory { get; set; }
        public string FileToProcess { get; set; }
        public string FileProcessed { get; set; }
        public string FileNoProcessed { get; set; }
    }

    public class XMSettings
    {
        public string User { get; set; }
        public string Pass { get; set; }
    }

    public class Auth
    {
        public string Token { get; set; }
        public string User { get; set; }
        public string Pass { get; set; }
    }

    public class IntegrationSettings
    {
        public List<ServicesIntegration> Services { set; get; }
    }

    public class ServicesIntegration
    {
        public string Name { set; get; }
        public string Url { set; get; }
        public Auth Authentication { set; get; }
        public List<Metodo> Methods { set; get; }
    }

    public class Metodo
    {
        public string Method { set; get; }
        public string Value { set; get; }
    }
}
