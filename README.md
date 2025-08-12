## System Overview and Initialization

### Introduction

The Employee Management System (EMS) is built with **ASP.NET Core (.NET 8)** using the **MVC architecture** and **Entity Framework Core**. The system implements a **hierarchical organizational structure** with **strict role-based access control** and **comprehensive audit capabilities**.

---

### Technology Stack

- **Framework:** ASP.NET Core (.NET 8) MVC
- **Database:** SQL Server with Entity Framework Core
- **Authentication:** ASP.NET Core Identity + JWT
- **Authorization:** Cookie-based with role validation
- **UI:** Razor Views

---

### System Initialization & Default Configuration

**Pre-configured System Setup:**

- **Email:** [defaultadmin@ems.com](mailto:defaultadmin@ems.com)
- **Password:** `Default@123`
- **Role:** Admin
- **Department:** Admin Department (auto-created)

**System Startup Sequence:**

1. Default Admin Department is automatically created
2. Default Admin Account is pre-configured and ready for first login
3. Admin must login first to initialize and configure the entire system
4. All subsequent operations follow strict hierarchical creation rules

---

### Organizational Hierarchy Rules

**Critical Business Rule – Creation Order:**

```
1. SYSTEM FOUNDATION
   ├── Admin Department (pre-created)
   └── Default Admin Account (pre-configured)

2. ADMIN INITIALIZATION PHASE
   ├── Admin logs in with default credentials
   ├── Admin creates additional departments
   └── Admin must exist before any Manager can be created

3. MANAGER CREATION PHASE
   ├── Admin creates Manager employee records
   ├── Each department can have ONLY ONE manager
   ├── Managers are assigned to specific departments
   └── Manager role automatically assigned upon department assignment

4. EMPLOYEE CREATION PHASE
   ├── Employees can ONLY be created if their department has a manager
   ├── Employees automatically assigned to their department's manager
   └── Employee.ManagerId links to department manager
```

---

## Database Design - ER Diagram

### Relationships Overview :

- **Employee → EmployeeLog**
    
    One-to-Many (Logs employee changes)
    
- **Department → DepartmentLogs**
    
    One-to-Many (Logs department changes)
    
- **Employee → Employee (Self-Join)**
    
    One-to-Many (Manager → Employees)
    
- **Department → Employee**
    
    One-to-Many (Employees under a department)
    
- **Department.ManagerId → Employee.EmployeeId**
    
    One-to-One (Department has a Manager)
    
- **Users → Employee**
    
    One-to-One (Account linked to Employee)
    
- **Employee → LeaveRequest**
    
    One-to-Many (Leave requests made)
    
- **Employee → LeaveBalance**
    
    One-to-One (Leave balance maintained)
    
- **Users → LoginActivityLogs**
    
    One-to-Many (Login activity per user)
    
- **Employee → LoginActivityLogs**
    
    One-to-Many (Login activity per employee)
    
- **AspNetRoles → AspNetUserRoles**
    
    One-to-Many (Role mappings)
    
- **Users → AspNetUserRoles**
    
    One-to-Many (User role mappings)
    
- **Users → UserActivityLog**
    
    One-to-Many (User system activity)
    

### ER Diagram :
![ER Diagram](https://github.com/tharuun12/EmployeeManagementSystem/blob/0e5d092bdb74f20a691a0907c878050f6eab950f/Final%20ER%20diagram%20For%20EMS.png)
---

# **Project Modules & Features**

## 1. User Authentication & Authorization

### Core Features Implemented

- User Registration (Admin & Employees)
- Login & Logout (JWT Authentication / Identity Framework)
- Role-based Authorization (Admin, Manager, Employee)
- Forgot Password & Reset Password

---

### User Registration (Admin & Employees)

**Implementation:** `AccountController.cs` – `Register` method

**Critical Business Logic:**

- **Employee Record Must Pre-exist:** Registration is allowed only if the employee record already exists in the database
- **Email-based Validation:** User email must match an existing employee email exactly
- **Role Inheritance:** User role is automatically inherited from the employee record
- **Account Linking:** `Employee.UserId` links to Identity `User.Id` for future operations

**Registration Flow:**

1. User attempts registration with email
2. System validates employee record exists with that email
3. If no employee record – Registration is blocked
4. If employee exists – Create Identity account
5. Link Identity account to employee record
6. Assign role based on `employee.Role`
7. Create activity log entry
8. Redirect to login page

**Security Measures:**

- Pre-validation against employee database
- Automatic role assignment prevents privilege escalation
- Complete activity logging for audit trails
- Employee-Identity account linking ensures system integrity

---

### Login & Logout (JWT Authentication / Identity Framework)

**Implementation:** `AccountController.cs` – `Login` and `Logout` methods

**Authentication Architecture:**

- Dual Authentication: JWT tokens and Cookie-based sessions
- JWT Configuration: 60-minute expiration with secure signing
- Cookie Security: `HttpOnly`, `Secure` flags enabled
- Session Management: Login/logout time tracking

**Login Process:**

1. Validate user credentials against Identity store
2. Check account lockout status
3. Verify password using Identity framework
4. Retrieve user roles from Identity
5. Generate JWT token with claims
6. Create authentication cookie
7. Link to employee record if not already linked using `UserID`
8. Create login activity log
9. Role-based redirection to appropriate dashboard based on role 

**Account Lockout System:**

- Soft Delete: When employee is deleted, the account is locked but not deleted
- Lockout Implementation: `LockoutEnabled = true`, `LockoutEnd = DateTimeOffset.MaxValue`
- Access Prevention: Locked accounts cannot login but data is preserved in `AspNetUsers`

**Role-based Redirection:**

- Admin/Manager → Redirected to `Manager/Index`
- Employee → Redirected to `Employee/Index`

---

### Role-based Authorization (Admin, Manager, Employee)

**Implementation:** Controller-level and Action-level `[Authorize]` attributes

**Permission Matrix:**

| Feature | Admin | Manager | Employee |
| --- | --- | --- | --- |
| Create Employees | Full Access | No Access | No Access |
| Edit Employees | Full Access | No Access | No Access |
| Delete Employees | Full Access | No Access | No Access |
| View Employees | All Employees | Department Only | Self Only |
| Create Departments | Full Access | No Access | No Access |
| Edit Departments | Full Access | No Access | No Access |
| Delete Departments | Full Access | No Access | No Access |
| View Departments | Full Access | No Access | No Access |
| Approve Leave | Any Employee | Direct Reports Only | No Access |
| Apply for Leave | Not Available  | Yes | Yes |

**Authorization Implementation:**

- **Controller Level:** `[Authorize(Roles = "Admin, Manager")]`
- **Action Level:** Specific role restrictions on sensitive methods
- **Data Filtering:** Role-based query logic
- **UI Controls:** Role-based visibility for menus and buttons

---

### Forgot Password & Reset Password

**Implementation:** `AccountController.cs` – OTP-based password reset system

**Password Reset Workflow:**

1. User requests password reset with email
2. System generates 6-digit OTP
3. OTP stored with 10-minute expiry in `TempData`
4. OTP sent via email (`IEmailService` integration)
5. User enters OTP for verification
6. System validates OTP and expiry time
7. If valid, user can set a new password
8. Password reset using Identity framework
9. Confirmation and redirect to login

**Security Features:**

- Time-limited OTP (10-minute expiration)
- Single-use validation: OTP is cleared after use
- Email verification ensures legitimacy
- Strong password policies enforced by Identity framework

---

## 2. Employee Management Module

### Core Features Implemented

- Add, Edit, Delete Employee Details
- Assign Employees to Departments
- View Employee Profiles
- Filter Employees by Department, Role

---

### Add, Edit, Delete Employee Details

**Implementation:** `EmployeeController.cs` – Full CRUD operations

### Critical Business Rules

**Employee Creation Rules:**

- **Admin-Only Access:** Only Admins can create employee records
- **Unique Email Validation:** Duplicate emails are not allowed
- **Department-Manager Dependency:** Employees can only be created if their department has an assigned manager
- **Role Hierarchy Enforcement:** At least one Admin must exist before any Manager can be added
- **Automatic Manager Assignment:** Employees are automatically assigned to their department's manager

**Creation Validation Logic:**

- **If Role == "Employee":**
    - If `Department.ManagerId == null`:
        - Block creation → *"Please assign Manager to Department first"*
- **If Role == "Manager":**
    - If department already has a manager:
        - Block creation → *"Department already has a manager"*
    - If no Admin exists in system:
        - Block creation → *"Please create Admin before adding Manager"*

---

### Employee Deletion Process

1. Check if the employee is a department manager
2. If manager → block deletion (to preserve data integrity)
3. Lock Identity account:
    - `LockoutEnabled = true`
    - `LockoutEnd = DateTimeOffset.MaxValue`
4. Clear all subordinate relationships
5. Remove the employee from department manager assignments
6. Create a comprehensive audit log
7. Delete the employee record from the database

---

### Edit Employee Logic

- **Role Change Handling:** Automatically update department relationships
- **Manager Promotion:** Assign department
- **Manager Demotion:** Clear all existing management relationships
- **Department Changes:** Cascade updates to manager assignments

---

### Assign Employees to Departments

**Implementation:** Department assignment logic with automatic manager linking

**Assignment Rules:**

- **Department Selection:** Provided as a dropdown of available departments
- **Automatic Manager Assignment:** `Employee.ManagerId = Department.ManagerId`
- **Manager Role Updates:** When an employee is assigned as department manager, their role is updated
- **Cascading Updates:** All employees in that department are updated to report to the new manager

---

### View Employee Profiles

**Implementation:** Role-based access to profiles

**Profile Access Rules:**

- **Admin:** Can view any employee profile
- **Manager:** Can view profiles of direct reports
- **Employee:** Can view only their own profile

**Profile Information Displayed:**

- Personal details (Name, Email, Phone)
- Department information
- Manager information (if applicable)
- Leave balance and history
- Current month leave summary

---

### Filter Employees by Department, Role

**Implementation:** `EmployeeController.cs` – Filter method

**Filtering Capabilities:**

- **Department Filter:** Show employees in a specific department
- **Role Filter:** Show employees with a specific role (Admin / Manager / Employee)
- **Combined Filtering:** Department + Role combinations
- **Admin/Manager Access:** Only Admins and Managers can apply filters
- **Dynamic Dropdowns:** Departments and roles are populated dynamically from the database

---

## 3. Department Management Module

### Core Features Implemented

- Add, Edit, Delete Departments (Admin Only)

---

### Add, Edit, Delete Departments

**Implementation:** `DepartmentController.cs` – CRUD operations

**Department Creation:**

- **Admin-Only Access:** Only users with the Admin role can create departments
- **Audit Logging:** All department operations are logged in `DepartmentLogs`
- **Validation:** Department name must be unique

---

### Department Editing – Manager Assignment Workflow

**When a Manager is assigned to a department:**

1. Update `Department.ManagerId` and `ManagerName`
2. Update employee's department assignment
3. Assign manager to **all** employees within the department (`Employee.ManagerId = Department.ManagerId`)
4. Create an audit log entry to track the update

---

### Employee–Department Relationship Management

**Implementation:** Managed within the department and employee controllers/services

**Assignment Process:**

- **Manager Validation:** Department must have a manager before employees can be assigned
- **Automatic Relationships:** `Employee.ManagerId` is automatically set based on the department’s assigned manager
- **Cascading Updates:** If department assignments change, employee-manager links are updated accordingly
- **Role Consistency:** When an employee becomes a department manager, their role is updated to "Manager" to ensure system consistency

---

## 4. Leave Management Module

### Core Features Implemented

- Employees can Apply for Leave
- Admin/Manager can Approve or Reject Leave Requests
- Track Leave Balance

---

### Employees can Apply for Leave

**Implementation:** `LeaveController.cs` – `Apply` method

**Application Flow:**

1. Employee accesses the leave application form
2. System identifies employee via `User.UserId → Employee.UserId` link
3. Employee submits leave request with date range and reason
4. System sets `Status = "Pending"` and `RequestDate = current time`
5. System checks for an existing `LeaveBalance` record (creates with 20 days if not found)
6. Leave request is saved to the database
7. Redirect to the employee's leave history page

**Leave Balance Management:**

- **Default Balance:** 20 days for all new employees
- **Automatic Creation:** `LeaveBalance` record is auto-created if missing
- **Dual Tracking:** Both `LeaveBalance` table and `Employee.LeaveBalance` field are maintained

---

### Admin/Manager can Approve or Reject Leave Requests

**Implementation:** Two-tier approval system

### Manager Approval (Department-Scoped)

**Controller:** `ManagerController.cs` – `ApproveList` method

Approval Flow:

1. Fetch current manager's employee record
2. Retrieve all employees where `ManagerId == current manager’s EmployeeId`
3. Display only leave requests from direct reports
4. Manager can approve or reject only their own team's requests

### Admin Approval (System-Wide)

**Controller:** `LeaveController.cs` – `ApproveList` method

Approval Flow:

1. Admin can view all pending leave requests
2. No department-level restrictions
3. Admin can approve or reject requests for any employee

---

### Approval Process

1. Select leave request for review
2. Choose status: **"Approved"** or **"Rejected"**

**If Approved:**

- Calculate leave days = `(EndDate - StartDate) + 1`
- Validate that sufficient leave balance exists
- Update `LeaveBalance.LeavesTaken += days`
- Update `Employee.LeaveBalance -= days`

**If Rejected after a prior approval:**

- Restore deducted leave balance
1. Update leave request status
2. Save all changes to the database

---

### Track Leave Balance

**Implementation:** Comprehensive tracking system

**Balance Tracking Features:**

- **Dual Storage:** Tracked in both `LeaveBalance` table and `Employee.LeaveBalance` field
- **Real-time Updates:** Balances updated upon approval or rejection
- **Validation:** System prevents approval if insufficient balance
- **Monthly Reports:** Employee can view leave usage for the current month
- **Historical Tracking:** Full leave request history is preserved

**Balance Calculation Rules:**

- **Initial Balance:** 20 days (configurable setting)
- **Deduction:** Applies only when leave requests are approved
- **Restoration:** If an approved leave is later rejected
- **Remaining Balance:** `TotalLeaves - LeavesTaken`

---

## 5. Reporting & Dashboard

### Core Features Implemented

- View total employees, active employees, and departments (Admin Side)
- Employee login history and recent activities
- Monthly Leave Report (Employee/Admin Side)

---

### View Total Employees, Active Employees, and Departments

**Implementation:** `DashboardController.cs` – Admin Dashboard

**Admin Dashboard Metrics:**

- **Total Employees:** Count of all employee records
- **Active Employees:** Employees where `IsActive = true`
- **Total Departments:** Count of all departments
- **Recent Employees:** Last 5 employees added
- **Department Statistics:** Distribution of employees across departments

**Data Visualization:**

- **Department Stats:** Employee count per department
- **Activity Overview:** Key system usage statistics
- **Quick Actions:** Shortcuts to major system functionalities

---

### Employee Login History and Recent Activities

**Implementation:** `ActivityController.cs` – Login tracking system

**Activity Tracking Features:**

- **Login History:** Tracks all login and logout events with timestamps
- **IP Address Logging:** Records client IP for each login session
- **Session Duration:** Duration calculated between login and logout
- **Failed Attempt Tracking:** Records invalid login attempts
- **User Activity Logs:** Tracks user interactions and page visits

**Login Activity Data:**

- **Login Time:** Exact time of successful login
- **Logout Time:** Session end time
- **IP Address:** Logged user’s device IP
- **Success Status:** Indicates login success or failure
- **Employee Linking:** Associates activity data with employee record

---

### Monthly Leave Report – Employee/Admin Side

**Implementation:** `EmployeeController.cs` – `CurrentMonthInfo` method

**Monthly Summary Flow:**

1. Identify current logged-in employee
2. Retrieve current month and year
3. Query leave requests within current month
4. Calculate total approved leave days
5. Determine remaining leave balance
6. Identify the reporting manager
7. Generate comprehensive summary for the month

**Report Contents:**

- **Employee Information:** Name, department, manager
- **Current Month:** Displayed as formatted Month/Year
- **Leave Requests:** All requests made in current month with their statuses
- **Days on Leave:** Total approved days within the month
- **Remaining Balance:** Leave days remaining after deduction
- **Manager Information:** Name of reporting manager

---

## 6. Security Implementation

### Authentication Security

- **JWT Tokens:** Time-limited (60 minutes) with secure signing
- **Cookie Security:** HttpOnly and Secure flags applied
- **Account Lockout:** Soft-deletion with `LockoutEnabled = true`, `LockoutEnd = MaxValue`
- **Password Policies:** Enforced by ASP.NET Identity
- **Session Management:** Complete tracking of login and logout lifecycle

---

### Authorization Hierarchy

- **Admin:** Full system access and user control
- **Manager:** Department-level control and leave approval
- **Employee:** Limited to personal profile and operations

---

### Data Protection

- **Role-based Filtering:** Ensures users can only access relevant data
- **Audit Logging:** Full activity logs across all modules
- **Input Validation:** Server-side model validation in all forms and APIs
- **CSRF Protection:** Anti-forgery tokens included in all forms
- **Relationship Integrity:** Updates cascade across related entities

---

### Business Rule Enforcement

- **Hierarchical Creation:** Enforced Admin → Manager → Employee sequence
- **Department Integrity:** Departments with employees cannot be deleted
- **Manager Protection:** Cannot delete an employee who is currently a department manager
- **Leave Balance Validation:** Leave approval blocked if balance is insufficient
- **Role Consistency:** Organizational structure changes trigger automatic role updates
