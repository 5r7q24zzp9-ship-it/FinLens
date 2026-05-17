namespace FinLens.Domain.Enums;

public enum WorkspaceType { Individual, Group, Corporate }
public enum WorkspaceMemberRole { Owner, Admin, Accountant, Employee, Member }
public enum TransactionType { Income, Expense }
public enum TransactionStatus { Active, Pending, Approved, Rejected, Cancelled }
public enum ApprovalStatus { Pending, Approved, Rejected, Escalated }
public enum ApprovalLevel { Accountant, Manager }
public enum NotificationChannel { WebSocket, Email, Push }
public enum BudgetPeriod { Daily, Weekly, Monthly, Yearly }
public enum RecurringFrequency { Daily, Weekly, Monthly, Yearly }
