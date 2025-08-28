# FitpriseVA – Database Schema (Authoritative Summary)

## Purpose
This document tells the AI **how to query our SQL Server DB safely**. It lists:
- Tables, primary keys, foreign keys
- Join paths (with preferred keys)
- Important columns and typical filters
- Aggregation rules and safe examples

**Hard rules for the AI:**
- **SELECT only**. Never use INSERT/UPDATE/DELETE/MERGE/EXEC/DDL.
- Return **≤ 6 columns** that are most informative for the user’s question.
- Use **explicit JOINs**.
- Use **TOP 200** unless the question needs fewer/more (but never remove limits entirely).
- Prefer **indexed/filterable columns** noted below.

---

## Entities & Relationships (high level)
- **Customers** (1) — **Orders** (many)
- **Orders** (1) — **OrderItems** (many)
- **Products** (1) — **OrderItems** (many)
- **Employees** (1) — **Orders** (many) [sales owner]
- **Invoices** (1:1 or 1:0..1) — **Orders**

---

## Tables

### 1) `dbo.Customer_Profile`
**PK:** `Customer_Profile_ID` (int, identity)  
**Important columns:**  
- `Customer_Profile_ID` (PK)  
- `Customer_Code` (nvarchar(255), unique)  
- `Customer_Name` (nvarchar(255))  
- `Status` (nvarchar(150)) — enum: `Active|Inactive`
**Typical filters:** `Customer_Code`, `Customer_Name` (LIKE 'abc%'), `Status` (='Active')

### 2) `dbo.Quotation`
**PK:** `Quotation_ID` (int, identity)  
**FKs:**  
- `Customer_Profile_ID` → `Customer_Profile.Customer_Profile_ID`  
- `Employee_Profile_ID` → `Employee_Profile.Employee_Profile_ID` 
- `Currency_Profile_ID` → `Currency_Profile.Currency_Profile_ID`
**Important columns:**  
- `Quotation_ID` (PK)  
- `Quotation_No` (nvarchar(255))  
- `Quotation_Title` (nvarchar(255))  
- `Issued_Date` (datetime)  
- `Due_Date` (datetime)  
- `Currency_Profile_ID` (int)  
- `Amount` (decimal(20,2))  
- `Tax_Amount` (decimal(20,2))  
- `Total_Amount` (decimal(20,2))  
- `Status` (nvarchar(150)) — enum: `Draft|Confirmed|Cancelled|Revised|Issued`
**Typical filters:** `Quotation_No`, `Quotation_Title`

### 3) `dbo.Quotation_Details`
**PK:** `Quotation_Details_ID` (int, identity)  
**FKs:**  
- `Quotation_ID` → `Quotation.Quotation_ID`  
- `UOM_Profile_ID` → `UOM_Profile.UOM_Profile_ID`  
**Important columns:**  
- `Quotation_Details_ID` (PK)  
- `Description` (ntext)  
- `Qty` (float)  
- `UOM_Profile_ID` (int)
- `Price` (decimal(20, 2))  
- `Total_Amount` (decimal(20, 2))  
- `Cancellation_Qty` (float)  
- `Cancellation_Amount` (decimal(20, 2))  
- `Final_Amount` (decimal(20, 2))  

### 4) `dbo.Currency_Profile`
**PK:** `Currency_Profile_ID` (int, identity) 
**Important columns:**  
- `Currency_Profile_ID` (PK)  
- `Currency_Code` (nvarchar(255))   
- `Currency_Name` (nvarchar(255))   
- `Exchange_Rate` (decimal(20,6))  
- `Status` (nvarchar(150)) — enum: `Active|Inactive`

### 5) `dbo.UOM_Profile`
**PK:** `UOM_Profile_ID` (int, identity)  
**Important columns:**  
- `UOM_Profile_ID` (PK)  
- `UOM` (nvarchar(50), unique)  
- `Status` (nvarchar(150)) — enum: `Active|Inactive`

### 6) `dbo.Employee_Profile`
**PK:** `Employee_Profile_ID` (int, identity)
**Important columns:**  
- `Employee_Profile_ID` (PK) 
- `Employee_Code` (nvarchar(255), unique)  
- `Employee_Name` (nvarchar(255))
- `Email` (nvarchar(255))
- `Mobile_Number` (nvarchar(255))

---

## Preferred Join Keys
- Customer_Profile ↔ Quotation: `c.Customer_Profile_ID = q.Quotation_ID`
- Quotation ↔ Quotation_Details: `q.Quotation_ID = qd.Quotation_ID`
- Quotation_Details ↔ UOM_Profile_ID: `qd.UOM_Profile_ID = u.UOM_Profile_ID`
- Quotation ↔ Currency_Profile: `q.Currency_Profile_ID = cp.Currency_Profile_ID`
- Quotation ↔ Employee_Profile: `q.Employee_Profile_ID = e.Employee_Profile_ID`

> Avoid joining on names/emails unless explicitly requested.

---

## Index Hints (for filter efficiency)
- `Customer_Profile.Customer_Profile_ID`, `Customer_Profile.Customer_Code`
- `Quotation_No`

*(Do not include index names; just list useful filter columns.)*

---

## Aggregation & Limits
- Always apply `TOP 100` to base queries unless user asks for a small set (e.g., “Top 10”).
- When aggregating revenue:
  - Use `SUM(qd.Qty * qd.Final_Price)` for line totals.
  - Grouping keys should be explicit (e.g., by `q.Due_Date`, `c.Customer_Profile_ID`).
- For date rollups, use **calendar-safe** groupings (e.g., `CONVERT(date, q.Issued_Date)`).

---

## Safe Example Queries (reference patterns)

**A) Recent quotations (basic listing)**
```sql
SELECT TOP 100 
  q.Quotation_No, q.Issued_Date, c.Customer_Name, cp.Currency_Code, q.Amount, q.Tax_Amount, q.Total_Amount
FROM dbo.Quotation q
JOIN dbo.Customer_Profile c ON c.Customer_Profile_ID = q.Customer_Profile_ID
LEFT JOIN Currency_Profile cp ON q.Currency_Profile_ID = cp.Currency_Profile_ID
WHERE q.Issued_Date >= DATEADD(day, -30, GETDATE())
AND q.Status IN ('Issued', 'Confirmed')
ORDER BY q.Issued_Date DESC;
