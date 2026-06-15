using PharmacyWarehouse.Data;
using PharmacyWarehouse.Models;

namespace PharmacyWarehouse.Services
{
    public class SystemInfoService : ObservableObject
    {
        private string _status = "Готов";
        private string _dbStatus = "Проверка...";
        private int _productCount;
        private string _lastSave = DateTime.Now.ToString("HH:mm:ss");
        private int _notificationCount;

        public string Status
        {
            get => _status;
            private set => SetProperty(ref _status, value);
        }

        public string DbStatus
        {
            get => _dbStatus;
            private set => SetProperty(ref _dbStatus, value);
        }

        public int ProductCount
        {
            get => _productCount;
            private set => SetProperty(ref _productCount, value);
        }

        public string LastSave
        {
            get => _lastSave;
            private set => SetProperty(ref _lastSave, value);
        }

        public int NotificationCount
        {
            get => _notificationCount;
            private set => SetProperty(ref _notificationCount, value);
        }

        public void SetNotificationCount(int count)
        {
            SetProperty(ref _notificationCount, count);
        }

        public void ResetNotificationCount()
        {
            SetProperty(ref _notificationCount, 0);
        }
        public SystemInfoService()
        {
        }

        public void UpdateInfo()
        {
            try
            {
                using var db = new PharmacyWarehouseContext();

                bool canConnect = db.Database.CanConnect();
                DbStatus = canConnect ? "Online" : "Offline";

                if (canConnect)
                {
                    ProductCount = db.Products.Count();
                    Status = "Работает";
                }
                else
                {
                    Status = "Ошибка БД";
                }

                LastSave = DateTime.Now.ToString("HH:mm:ss");
            }
            catch
            {
                Status = "Ошибка";
                DbStatus = "Ошибка";
            }
        }

        public void SetStatus(string status)
        {
            Status = status;
            LastSave = DateTime.Now.ToString("HH:mm:ss");
        }
    }
}
