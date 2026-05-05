# 4+1 Software Architecture Document (SAD) — Phase 1
## Dental Clinic Online Appointment & Management System

---

> **Document Version:** 1.0  
> **Date:** 2026-04-30  
> **Methodology:** Kruchten's 4+1 Architectural View Model  
> **Technology Stack:** ASP.NET Core MVC · Entity Framework Core · SQL Server · Bootstrap 5

---

## 0. System Selection

### 0.1 System Definition
The selected system is a **Dental Clinic Online Appointment & Management System** designed to support appointment operations, role-based access, and core clinic administration in a single web platform.

### 0.2 Purpose of the System
The system aims to digitalize and coordinate daily clinic workflows that are traditionally handled manually (phone booking, paper-based schedule tracking, and fragmented payment records). It provides a structured software solution aligned with modern architecture principles.

### 0.3 Target Users
- **Secretary (Admin):** manages doctors, appointments, services, customers, and payments.
- **Doctor:** monitors personal schedule, patient notes, and appointment completion status.
- **Customer (Patient):** registers, logs in, books appointments, and tracks upcoming/past visits.
- **System (Automated):** executes reminder generation as an internal background process.

### 0.4 Main Functionalities
- Role-based authentication and authorization.
- Appointment slot browsing and booking.
- Appointment cancellation and completion lifecycle.
- Secretary-side calendar, doctor management, and payment recording.
- Revenue and earnings visibility based on appointment/payment data.
- Automated in-app reminder generation for upcoming appointments.
- Slot conflict prevention and business-rule enforcement.

---

## 1. Use Case View (Scenarios View)

The Use Case View captures the **functional requirements** from each actor's perspective. It serves as the central document that drives all other architectural views.

### 1.1 System Actors

| Actor | Role | Authority Level |
|-------|------|-----------------|
| **Secretary (Admin)** | System administrator and clinic operations manager | Full system access |
| **Doctor** | Licensed dental practitioner | Restricted to own schedule & data |
| **Customer (Patient)** | Registered clinic patient | Self-service appointment management |
| **System (Automated)** | Background job / scheduler | Internal only — no login |

---

### 1.2 Use Cases by Actor

#### 🧑‍💼 Secretary (Admin) Use Cases

| ID | Use Case | Description |
|----|----------|-------------|
| UC-S01 | **Login / Logout** | Authenticates into the admin panel using credentials. |
| UC-S02 | **Manage Doctors** | Creates, updates, activates, or deactivates doctor profiles. |
| UC-S03 | **View All Calendars** | Views the daily/weekly appointment calendar for all doctors simultaneously. |
| UC-S04 | **Create Manual Appointment** | Creates a new appointment on behalf of a patient (phone-in booking). |
| UC-S05 | **Cancel / Reschedule Appointment** | Cancels or moves any appointment in the system. |
| UC-S06 | **Record Payment** | Records a payment against a completed appointment, entering amount and method. |
| UC-S07 | **Generate Invoice** | Creates a printable or PDF invoice for a patient's visit. |
| UC-S08 | **View Payment Dashboard** | Displays total revenue, outstanding payments, and per-doctor earnings summaries. |
| UC-S09 | **Manage Customers** | Views patient list, can deactivate accounts or reset passwords. |
| UC-S10 | **Manage Services / Treatments** | Defines available dental treatments and their base fees. |

---

#### 🦷 Doctor Use Cases

| ID | Use Case | Description |
|----|----------|-------------|
| UC-D01 | **Login / Logout** | Authenticates into the doctor's private dashboard. |
| UC-D02 | **View Daily Schedule** | Views the list of today's appointments in chronological order. |
| UC-D03 | **View Weekly Calendar** | Views a 7-day calendar of their upcoming appointments. |
| UC-D04 | **View Patient Notes** | Reads notes attached to each upcoming appointment by the patient or secretary. |
| UC-D05 | **View Earnings Summary** | Views their calculated fee/commission for the current day or week based on completed appointments. |
| UC-D06 | **Mark Appointment as Completed** | Changes appointment status to "Completed" after the patient's visit. |

---

#### 👤 Customer (Patient) Use Cases

| ID | Use Case | Description |
|----|----------|-------------|
| UC-C01 | **Register** | Creates a new patient account with name, contact info, and password. |
| UC-C02 | **Login / Logout** | Authenticates into the patient portal. |
| UC-C03 | **Browse Available Slots** | Selects a doctor, a date, and views all unbooked time slots. |
| UC-C04 | **Book Appointment** | Reserves a selected time slot, optionally adding a complaint note. |
| UC-C05 | **Cancel Appointment** | Cancels an upcoming appointment (subject to cancellation policy). |
| UC-C06 | **View Upcoming Appointments** | Sees a list of all future confirmed appointments with reminders. |
| UC-C07 | **View Appointment History** | Reviews all past completed/cancelled appointments. |
| UC-C08 | **View Payment History** | Lists all invoices and payments associated with their account. |
| UC-C09 | **Update Profile** | Changes personal details or password. |

---

#### ⚙️ System (Automated) Use Cases

| ID | Use Case | Description |
|----|----------|-------------|
| UC-SYS01 | **Send Appointment Reminders** | Background job that checks for appointments within 24 hours and creates in-app notification records. |
| UC-SYS02 | **Enforce Slot Conflict Check** | Automatically rejects booking requests that overlap with an existing confirmed appointment for the same doctor. |

---

### 1.3 Use Case Diagram (Textual UML Representation)

```mermaid
flowchart LR
    Secretary["Secretary (Admin)"]
    Doctor["Doctor"]
    Customer["Customer (Patient)"]
    System["System (Automated)"]

    subgraph SEC["Secretary Portal Use Cases"]
        UC_S02["Manage Doctors"]
        UC_S03["View All Calendars"]
        UC_S04["Create Manual Appointment"]
        UC_S05["Cancel / Reschedule Appointment"]
        UC_S06["Record Payment"]
        UC_S08["View Payment Dashboard"]
        UC_S09["Manage Customers"]
        UC_S10["Manage Services / Treatments"]
    end

    subgraph DOC["Doctor Portal Use Cases"]
        UC_D02["View Daily Schedule"]
        UC_D03["View Weekly Calendar"]
        UC_D04["View Patient Notes"]
        UC_D05["View Earnings Summary"]
        UC_D06["Mark Appointment as Completed"]
    end

    subgraph CUS["Customer Portal Use Cases"]
        UC_C01["Register"]
        UC_C02["Login / Logout"]
        UC_C03["Browse Available Slots"]
        UC_C04["Book Appointment"]
        UC_C05["Cancel Appointment"]
        UC_C06["View Upcoming Appointments"]
        UC_C07["View Appointment History"]
        UC_C08["View Payment History"]
        UC_C09["Update Profile"]
    end

    subgraph SYS["Automated Use Cases"]
        UC_SYS01["Send Appointment Reminders"]
        UC_SYS02["Enforce Slot Conflict Check"]
    end

    Secretary --> UC_S02
    Secretary --> UC_S03
    Secretary --> UC_S04
    Secretary --> UC_S05
    Secretary --> UC_S06
    Secretary --> UC_S08
    Secretary --> UC_S09
    Secretary --> UC_S10

    Doctor --> UC_D02
    Doctor --> UC_D03
    Doctor --> UC_D04
    Doctor --> UC_D05
    Doctor --> UC_D06

    Customer --> UC_C01
    Customer --> UC_C02
    Customer --> UC_C03
    Customer --> UC_C04
    Customer --> UC_C05
    Customer --> UC_C06
    Customer --> UC_C07
    Customer --> UC_C08
    Customer --> UC_C09

    System --> UC_SYS01
    System --> UC_SYS02

    classDef actor fill:#16324f,color:#ffffff,stroke:#16324f,stroke-width:1px;
    classDef usecase fill:#e8f0fb,color:#1a2b3c,stroke:#4b6b8a,stroke-width:1px;
    class Secretary,Doctor,Customer,System actor;
    class UC_S02,UC_S03,UC_S04,UC_S05,UC_S06,UC_S08,UC_S09,UC_S10,UC_D02,UC_D03,UC_D04,UC_D05,UC_D06,UC_C01,UC_C02,UC_C03,UC_C04,UC_C05,UC_C06,UC_C07,UC_C08,UC_C09,UC_SYS01,UC_SYS02 usecase;
```

### 1.4 Partial UI Implementation (Stage 1 Scope)

As required in Stage 1, partial UI implementation is represented by role-oriented interface modules:
- **Portal Entry & Role Login Screens:** separate access paths for Secretary, Doctor, and Customer.
- **Customer UI Flow:** appointment booking form (doctor/service/date-time selection), upcoming appointments view, and history navigation.
- **Doctor UI Flow:** dashboard-oriented schedule view and appointment status update action.
- **Secretary UI Flow:** dashboard, doctor creation/management, appointment handling, and payment recording interface.

The UI structure is organized under role-based view folders as documented in the project structure (e.g., `Views/Account`, `Views/Appointment`, `Views/Doctor`, `Views/Secretary`), which demonstrates partial front-end implementation aligned with use cases.

### 1.5 Business Rules

| BR | Rule |
|----|------|
| **BR-01** | No two appointments can be booked for the same Doctor at the same date and time. This is enforced at both the application layer (validation) and the database layer (unique constraint). |
| **BR-02** | A Customer can only cancel an appointment that is in `Pending` or `Confirmed` status. Completed appointments cannot be cancelled. |
| **BR-03** | A payment can only be recorded against an appointment in `Completed` status. |
| **BR-04** | A Doctor can only view appointments assigned to their own `DoctorId`; cross-doctor data is inaccessible. |
| **BR-05** | The system automatically generates a reminder notification for any `Confirmed` appointment 24 hours before its scheduled time. |
| **BR-06** | Doctor commission/earnings are calculated as: `AppointmentFee × DoctorCommissionRate`. |

---

### 1.6 Related Code Snippets (Partial UI + Interaction)

#### Snippet 1 — Booking UI Form (Partial Frontend)

```cshtml
<form asp-action="Book" method="post" id="booking-form">
    @Html.AntiForgeryToken()

    <select asp-for="DoctorId" class="form-select" id="select-doctor"
            asp-items="@(new SelectList(Model.AvailableDoctors, "DoctorId", "DisplayName"))">
        <option value="">— Choose a doctor —</option>
    </select>

    <select asp-for="ServiceId" class="form-select" id="select-service"
            asp-items="@(new SelectList(Model.AvailableServices, "ServiceId", "DisplayName"))">
        <option value="">— Choose a service —</option>
    </select>

    <input type="hidden" asp-for="AppointmentDate" id="selected-slot-input" />
    <button type="submit" class="btn btn-dc-primary">Confirm Booking</button>
</form>
```

#### Snippet 2 — Use Case Endpoint Mapping (Book + Slots)

```csharp
[HttpGet]
public async Task<IActionResult> Book()
{
    // populate doctors/services for booking UI
    return View(model);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> Book(BookingViewModel model)
{
    var appointment = await _bookingService.CreateAppointmentAsync(model, userId);
    if (appointment == null) return View(model);
    return RedirectToAction(nameof(Confirmed), new { id = appointment.AppointmentId });
}

[HttpGet]
public async Task<IActionResult> Slots(int doctorId, DateTime date)
{
    var slots = await _bookingService.GetAvailableSlotsAsync(doctorId, date);
    return Json(slots.Select(s => s.ToString("HH:mm")));
}
```

---

## 2. Logical View

The Logical View describes the **static structure** of the system — the key domain entities, their attributes, and the relationships between them.

### 2.1 Entity Descriptions

#### `ApplicationUser` *(Identity base — extends ASP.NET Core IdentityUser)*

The central identity entity. All actors (Secretary, Doctor, Customer) are stored as `ApplicationUser` records differentiated by **Role**.

| Field | Type | Description |
|-------|------|-------------|
| `Id` | `string` (GUID) | Primary Key (inherited from IdentityUser) |
| `FullName` | `string` | Display name |
| `PhoneNumber` | `string` | Contact phone |
| `DateOfBirth` | `DateTime?` | Optional — for patient records |
| `CreatedAt` | `DateTime` | Account creation timestamp |
| `IsActive` | `bool` | Soft-delete flag |

**Roles:** `Secretary`, `Doctor`, `Customer` — managed via `AspNetRoles`.

---

#### `Doctor`

A profile entity that extends `ApplicationUser` with doctor-specific attributes. Linked 1-to-1 to an `ApplicationUser` in the `Doctor` role.

| Field | Type | Description |
|-------|------|-------------|
| `DoctorId` | `int` | Primary Key |
| `UserId` | `string` | FK → `ApplicationUser.Id` |
| `Specialty` | `string` | e.g., "Orthodontics", "Endodontics" |
| `CommissionRate` | `decimal` | Percentage of appointment fee earned (e.g., 0.70 = 70%) |
| `WorkingHoursStart` | `TimeSpan` | Start of daily working hours |
| `WorkingHoursEnd` | `TimeSpan` | End of daily working hours |
| `SlotDurationMinutes` | `int` | Default appointment duration (e.g., 30 mins) |

**Relationships:**
- 1 Doctor → N Appointments
- 1 Doctor → N DoctorLeave records

---

#### `Customer`

A profile entity for patients. Linked 1-to-1 to an `ApplicationUser` in the `Customer` role.

| Field | Type | Description |
|-------|------|-------------|
| `CustomerId` | `int` | Primary Key |
| `UserId` | `string` | FK → `ApplicationUser.Id` |
| `MedicalNotes` | `string?` | Secretary-entered patient medical background |

**Relationships:**
- 1 Customer → N Appointments
- 1 Customer → N Notifications

---

#### `Service` *(Treatment / Procedure)*

Represents dental treatments offered by the clinic.

| Field | Type | Description |
|-------|------|-------------|
| `ServiceId` | `int` | Primary Key |
| `Name` | `string` | e.g., "Root Canal", "Teeth Whitening" |
| `Description` | `string?` | Details of the service |
| `BaseFee` | `decimal` | Standard price for this service |
| `DurationMinutes` | `int` | Estimated appointment duration |
| `IsActive` | `bool` | Toggle service availability |

---

#### `Appointment`

The core transactional entity of the system.

| Field | Type | Description |
|-------|------|-------------|
| `AppointmentId` | `int` | Primary Key |
| `CustomerId` | `int` | FK → `Customer.CustomerId` |
| `DoctorId` | `int` | FK → `Doctor.DoctorId` |
| `ServiceId` | `int` | FK → `Service.ServiceId` |
| `AppointmentDate` | `DateTime` | The scheduled date and time of the visit |
| `Status` | `enum` | `Pending`, `Confirmed`, `Completed`, `Cancelled` |
| `PatientNote` | `string?` | Patient's complaint or note at time of booking |
| `SecretaryNote` | `string?` | Internal note added by the secretary |
| `Fee` | `decimal` | Actual fee charged (may differ from `BaseFee`) |
| `CreatedAt` | `DateTime` | When the appointment was booked |
| `CreatedByUserId` | `string?` | FK — null if self-booked, set if secretary created it |

**Business Logic:** A **unique composite constraint** on `(DoctorId, AppointmentDate)` enforces BR-01.

**Relationships:**
- N Appointments → 1 Doctor
- N Appointments → 1 Customer
- N Appointments → 1 Service
- 1 Appointment → 0..1 Payment

---

#### `Payment`

Records financial transactions for completed appointments.

| Field | Type | Description |
|-------|------|-------------|
| `PaymentId` | `int` | Primary Key |
| `AppointmentId` | `int` | FK → `Appointment.AppointmentId` (unique) |
| `Amount` | `decimal` | Amount paid |
| `PaymentMethod` | `enum` | `Cash`, `CreditCard`, `BankTransfer` |
| `PaidAt` | `DateTime` | Transaction timestamp |
| `RecordedByUserId` | `string` | FK → `ApplicationUser.Id` (the secretary) |
| `InvoiceNumber` | `string` | Auto-generated invoice reference |

**Relationships:**
- 1 Payment → 1 Appointment *(1-to-1)*

---

#### `Notification`

In-app alerts sent to customers (system-generated reminders).

| Field | Type | Description |
|-------|------|-------------|
| `NotificationId` | `int` | Primary Key |
| `CustomerId` | `int` | FK → `Customer.CustomerId` |
| `AppointmentId` | `int?` | FK → `Appointment.AppointmentId` |
| `Message` | `string` | Notification text |
| `IsRead` | `bool` | Read status |
| `CreatedAt` | `DateTime` | When the notification was generated |

---

#### `DoctorLeave`

Tracks doctor unavailability (vacation, conference, etc.).

| Field | Type | Description |
|-------|------|-------------|
| `LeaveId` | `int` | Primary Key |
| `DoctorId` | `int` | FK → `Doctor.DoctorId` |
| `StartDate` | `DateTime` | Leave start |
| `EndDate` | `DateTime` | Leave end |
| `Reason` | `string?` | Optional explanation |

---

### 2.2 Entity Relationship Summary

```
ApplicationUser (1) ──────── (1) Doctor
ApplicationUser (1) ──────── (1) Customer

Doctor     (1) ──────── (N) Appointment
Customer   (1) ──────── (N) Appointment
Service    (1) ──────── (N) Appointment

Appointment (1) ──────── (0..1) Payment
Customer    (1) ──────── (N)    Notification
Doctor      (1) ──────── (N)    DoctorLeave
```

### 2.3 Entity Relationship Diagram (Visual)

```mermaid
erDiagram
    APPLICATION_USER ||--o| DOCTOR : has_profile
    APPLICATION_USER ||--o| CUSTOMER : has_profile
    DOCTOR ||--o{ APPOINTMENT : assigned_to
    CUSTOMER ||--o{ APPOINTMENT : books
    SERVICE ||--o{ APPOINTMENT : includes
    APPOINTMENT ||--o| PAYMENT : has_payment
    CUSTOMER ||--o{ NOTIFICATION : receives
    DOCTOR ||--o{ DOCTOR_LEAVE : has_leave

    APPLICATION_USER {
        string Id PK
        string FullName
        string Email
        string PhoneNumber
        datetime CreatedAt
        boolean IsActive
    }
    DOCTOR {
        int DoctorId PK
        string UserId FK
        string Specialty
        decimal CommissionRate
        time WorkingHoursStart
        time WorkingHoursEnd
        int SlotDurationMinutes
    }
    CUSTOMER {
        int CustomerId PK
        string UserId FK
        string MedicalNotes
    }
    SERVICE {
        int ServiceId PK
        string Name
        decimal BaseFee
        int DurationMinutes
        boolean IsActive
    }
    APPOINTMENT {
        int AppointmentId PK
        int CustomerId FK
        int DoctorId FK
        int ServiceId FK
        datetime AppointmentDate
        string Status
        decimal Fee
        boolean ReminderSent
    }
    PAYMENT {
        int PaymentId PK
        int AppointmentId FK
        decimal Amount
        string PaymentMethod
        datetime PaidAt
        string InvoiceNumber
    }
    NOTIFICATION {
        int NotificationId PK
        int CustomerId FK
        int AppointmentId FK
        string Message
        boolean IsRead
        datetime CreatedAt
    }
    DOCTOR_LEAVE {
        int LeaveId PK
        int DoctorId FK
        datetime StartDate
        datetime EndDate
        string Reason
    }
```

### 2.4 Class Diagram (Mermaid)

```mermaid
classDiagram
    class ApplicationUser {
        +string Id
        +string FullName
        +string PhoneNumber
        +DateTime CreatedAt
        +bool IsActive
    }

    class Doctor {
        +int DoctorId
        +string UserId
        +string Specialty
        +decimal CommissionRate
        +TimeSpan WorkingHoursStart
        +TimeSpan WorkingHoursEnd
        +int SlotDurationMinutes
    }

    class Customer {
        +int CustomerId
        +string UserId
        +string MedicalNotes
    }

    class Service {
        +int ServiceId
        +string Name
        +decimal BaseFee
        +int DurationMinutes
        +bool IsActive
    }

    class Appointment {
        +int AppointmentId
        +int CustomerId
        +int DoctorId
        +int ServiceId
        +DateTime AppointmentDate
        +AppointmentStatus Status
        +string PatientNote
        +decimal Fee
    }

    class Payment {
        +int PaymentId
        +int AppointmentId
        +decimal Amount
        +PaymentMethod PaymentMethod
        +DateTime PaidAt
        +string InvoiceNumber
    }

    class Notification {
        +int NotificationId
        +int CustomerId
        +int AppointmentId
        +string Message
        +bool IsRead
    }

    class DoctorLeave {
        +int LeaveId
        +int DoctorId
        +DateTime StartDate
        +DateTime EndDate
    }

    ApplicationUser "1" --> "0..1" Doctor
    ApplicationUser "1" --> "0..1" Customer
    Doctor "1" --> "N" Appointment
    Customer "1" --> "N" Appointment
    Service "1" --> "N" Appointment
    Appointment "1" --> "0..1" Payment
    Customer "1" --> "N" Notification
    Doctor "1" --> "N" DoctorLeave
```

### 2.5 Backend Structure and Partial Backend Implementation

The backend is organized with clear responsibility boundaries:
- **Controllers Layer:** handles HTTP requests and role-specific endpoints (`AccountController`, `AppointmentController`, `SecretaryController`, `DoctorController`, `NotificationController`).
- **Service Layer:** encapsulates business logic (`BookingService`, `PaymentService`, `ReminderBackgroundService`) and enforces core business rules.
- **Data Access Layer:** `AppDbContext` manages entity sets, relationships, constraints, and migrations.
- **Identity/Authorization Layer:** `ApplicationUser` + role model (`Secretary`, `Doctor`, `Customer`) provides role-scoped access.

Partial backend implementation in Stage 1 is evidenced by:
- appointment booking logic with slot and leave validation,
- payment recording logic with completed-status and uniqueness checks,
- background reminder workflow logic through hosted service behavior,
- domain entities and relationship definitions mapped to persistent storage.

---

### 2.6 Related Code Snippets (Partial Backend)

#### Snippet — Entity Relationship and Constraint Definitions

```csharp
builder.Entity<Appointment>()
    .HasOne(a => a.Doctor)
    .WithMany(d => d.Appointments)
    .HasForeignKey(a => a.DoctorId)
    .OnDelete(DeleteBehavior.Restrict);

builder.Entity<Appointment>()
    .HasIndex(a => new { a.DoctorId, a.AppointmentDate })
    .IsUnique()
    .HasDatabaseName("UX_Appointment_Doctor_DateTime");

builder.Entity<Payment>()
    .HasIndex(p => p.AppointmentId)
    .IsUnique()
    .HasDatabaseName("UX_Payment_AppointmentId");
```

---

## 3. Process View

The Process View captures the **dynamic behavior** of the system — the key workflows showing how components interact at runtime.

### 3.1 Process 1: Customer Books an Appointment (End-to-End)

This is the primary happy-path workflow.

**Actors involved:** Customer, System, Database

```mermaid
sequenceDiagram
    actor Customer
    participant UI as Web UI
    participant Ctrl as AppointmentController
    participant Svc as BookingService
    participant DB as Database

    Customer->>UI: Open booking page
    UI->>Ctrl: GET /Appointment/Book
    Ctrl->>DB: Query doctors + services + booked slots
    DB-->>Ctrl: Result sets
    Ctrl-->>UI: Booking form with selectable slots
    UI-->>Customer: Show available time slots

    Customer->>UI: Submit booking form
    UI->>Ctrl: POST /Appointment/Book
    Ctrl->>Svc: CreateAppointmentAsync(model)
    Svc->>DB: Validate conflict (DoctorId + DateTime)
    DB-->>Svc: Slot free/taken
    Svc->>DB: Validate doctor leave overlap
    DB-->>Svc: Available/on leave

    alt Slot available and doctor available
        Svc->>DB: INSERT Appointment (Confirmed)
        Svc->>DB: INSERT Notification (confirmation)
        DB-->>Svc: Saved (AppointmentId)
        Svc-->>Ctrl: Success
        Ctrl-->>UI: Redirect /Appointment/Confirmed
        UI-->>Customer: Booking confirmed
    else Slot conflict or leave
        Svc-->>Ctrl: Validation failure
        Ctrl-->>UI: Return form with error
        UI-->>Customer: "Slot unavailable"
    end
```

#### 3.1.1 Activity Diagram: Booking Decision Flow

```mermaid
flowchart TD
    A([Customer login]) --> B[Open booking form]
    B --> C[Select doctor, service, date, time]
    C --> D{Model valid?}
    D -- No --> E[Show validation errors]
    E --> C
    D -- Yes --> F{Slot conflict?}
    F -- Yes --> G[Show slot unavailable message]
    G --> C
    F -- No --> H{Doctor on leave?}
    H -- Yes --> I[Show unavailable due to leave]
    I --> C
    H -- No --> J[Create appointment as Confirmed]
    J --> K[Create confirmation notification]
    K --> L([Redirect to confirmation page])
```

**Step-by-Step Narrative:**

1. **Customer navigates** to the booking page and selects a `Doctor` and a `Date`.
2. **System queries** the database for all `Confirmed` appointments for that doctor on that date and generates a list of **available time slots** based on the doctor's `WorkingHoursStart`, `WorkingHoursEnd`, and `SlotDurationMinutes`.
3. **Customer selects** a slot, picks a `Service`, and optionally types a `PatientNote`.
4. **Customer submits** the form via `POST /Appointment/Book`.
5. **`BookingService.CreateAppointmentAsync()`** runs the following validation pipeline:
   - `ModelState.IsValid` check (server-side validation)
   - Conflict check: queries `Appointments` table for existing `(DoctorId, AppointmentDate)` match
   - Leave check: queries `DoctorLeave` for overlapping dates
6. If **conflict detected** → returns `400 Bad Request` with an error message ("This slot is no longer available").
7. If **slot is free** → creates a new `Appointment` record with `Status = Confirmed`.
8. **System simultaneously creates** a `Notification` record for the customer: *"Your appointment with Dr. [Name] on [Date] at [Time] is confirmed."*
9. **Customer is redirected** to their upcoming appointments view with a success notification.

#### Related Code Snippet — Booking Workflow Logic

```csharp
bool slotTaken = await _db.Appointments.AnyAsync(a =>
    a.DoctorId == model.DoctorId
    && a.AppointmentDate == model.AppointmentDate
    && a.Status != AppointmentStatus.Cancelled);
if (slotTaken) return null;

bool onLeave = await _db.DoctorLeaves.AnyAsync(l =>
    l.DoctorId == model.DoctorId
    && l.StartDate.Date <= model.AppointmentDate.Date
    && l.EndDate.Date >= model.AppointmentDate.Date);
if (onLeave) return null;

_db.Appointments.Add(appointment);
await _db.SaveChangesAsync();

_db.Notifications.Add(notification);
await _db.SaveChangesAsync();
```

---

### 3.2 Process 2: Secretary Records a Payment

**Actors involved:** Secretary, System, Database

```mermaid
sequenceDiagram
    actor Secretary
    participant UI as Secretary UI
    participant Ctrl as SecretaryController
    participant Svc as PaymentService
    participant DB as Database

    Secretary->>UI: Open payment record page
    UI->>Ctrl: GET /Secretary/RecordPayment/{appointmentId}
    Ctrl->>DB: Query appointment + payment status
    DB-->>Ctrl: Appointment data
    Ctrl-->>UI: Payment form (amount/method)

    Secretary->>UI: Submit payment
    UI->>Ctrl: POST /Secretary/RecordPayment
    Ctrl->>Svc: RecordPaymentAsync(model)
    Svc->>DB: Validate appointment is Completed
    DB-->>Svc: Status result
    Svc->>DB: Validate no existing payment
    DB-->>Svc: Duplicate check result

    alt Valid and unpaid appointment
        Svc->>DB: INSERT Payment
        Svc->>DB: Generate and persist invoice number
        DB-->>Svc: Saved
        Svc-->>Ctrl: InvoiceNumber
        Ctrl-->>UI: Redirect Payments + success message
        UI-->>Secretary: Payment recorded
    else Invalid status or already paid
        Svc-->>Ctrl: Failure
        Ctrl-->>UI: Return form with error
        UI-->>Secretary: "Payment could not be recorded"
    end
```

**Step-by-Step Narrative:**

1. Secretary navigates to the **"Record Payment"** page.
2. System loads all `Completed` appointments that do **not** yet have an associated `Payment` record.
3. Secretary selects an appointment, enters `Amount`, and selects `PaymentMethod`.
4. On submit, `PaymentService.RecordPaymentAsync()` validates:
   - Appointment status is `Completed`
   - No existing `Payment` for this `AppointmentId` (prevents double-payment)
5. A `Payment` record is created with an auto-generated `InvoiceNumber` (format: `INV-YYYYMMDD-{ID}`).
6. Secretary is shown a printable invoice preview.

#### Related Code Snippet — Payment Processing Logic

```csharp
if (appointment.Status != AppointmentStatus.Completed) return null;
if (appointment.Payment != null) return null;

var invoiceNumber = $"INV-{DateTime.UtcNow:yyyyMMdd}-{appointment.AppointmentId:D5}";

var payment = new Payment
{
    AppointmentId = model.AppointmentId,
    Amount = model.Amount,
    PaymentMethod = model.PaymentMethod,
    PaidAt = DateTime.UtcNow,
    RecordedByUserId = secretaryUserId,
    InvoiceNumber = invoiceNumber
};

_db.Payments.Add(payment);
await _db.SaveChangesAsync();
```

---

### 3.3 Process 3: Automated Reminder Notification (Background Job)

**Actors involved:** System (Background Scheduler), Database, Customer (passive recipient)

```mermaid
sequenceDiagram
    participant Job as ReminderBackgroundService
    participant DB as Database
    actor Customer
    participant UI as Customer UI

    loop Every 60 minutes
        Job->>DB: Query confirmed appointments in next 24h\nwhere ReminderSent = false
        DB-->>Job: Upcoming appointment list
        alt Appointments found
            Job->>DB: INSERT Notification per appointment
            Job->>DB: UPDATE Appointment.ReminderSent = true
            DB-->>Job: Save complete
        else No records
            DB-->>Job: Empty set
        end
    end

    Customer->>UI: Open portal
    UI->>DB: Request unread notification count
    DB-->>UI: Count and notification list
    UI-->>Customer: Badge + reminder visibility
```

**Implementation Note:** The background job is implemented as an `IHostedService` registered in `Program.cs`. It runs on a configurable `Timer` interval (default: every 60 minutes). The `Appointment` entity includes a `ReminderSent` boolean flag to prevent duplicate notifications.

#### Related Code Snippet — Automated Reminder Workflow

```csharp
var upcomingAppointments = await db.Appointments
    .Where(a => a.Status == AppointmentStatus.Confirmed
             && !a.ReminderSent
             && a.AppointmentDate >= now
             && a.AppointmentDate <= cutoff)
    .ToListAsync();

foreach (var appointment in upcomingAppointments)
{
    db.Notifications.Add(notification);
    appointment.ReminderSent = true;
}

if (upcomingAppointments.Any())
    await db.SaveChangesAsync();
```

---
## Summary — Stage 1 Coverage

This Stage 1 submission includes:
- **System Selection** (system definition, purpose, target users, and core functionalities),
- **Use Case View** (actors, use cases, interactions, business rules, and partial UI scope),
- **Logical View** (entity model, class diagram, component responsibilities, and partial backend scope),
- **Process View** (workflow behavior for booking, payment, and automated reminders).

Accordingly, the report is aligned with Stage 1 expectations: architectural foundation plus partial frontend and backend implementation evidence.
