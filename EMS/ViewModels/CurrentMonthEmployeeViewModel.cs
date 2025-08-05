using EMS.Models;
using System.Collections.Generic;

namespace EMS.ViewModels
{
    public class CurrentMonthEmployeeViewModel
    {
        public Employee Employee { get; set; }
        public string ManagerName { get; set; }
        public string CurrentMonth { get; set; }
        public List<LeaveRequest> LeaveRequests { get; set; }
        public int DaysOnLeave { get; set; }
        public int RemainingLeaveBalance { get; set; }
    }
}