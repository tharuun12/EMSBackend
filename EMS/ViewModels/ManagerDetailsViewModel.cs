using Microsoft.AspNetCore.Mvc;

namespace EMS.ViewModels
{
    public class ManagerDetailsViewModel
    {
        public int EmployeeId { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public int? DepartmentId { get; set; }
        public string DepartmentName { get; set; }
    }

}
