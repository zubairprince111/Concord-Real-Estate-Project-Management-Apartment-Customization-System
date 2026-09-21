using System;
using System.Collections.Generic;

namespace Final.Models
{
    // A unit booking with running totals, used on the Client Payments screen.
    public class ClientBookingModel
    {
        public int BookingID { get; set; }
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }
        public string BuildingName { get; set; }
        public string FlatNumber { get; set; }
        public string ClientName { get; set; }
        public DateTime BookingDate { get; set; }
        public decimal TotalPrice { get; set; }
        public decimal TotalPaid { get; set; }
        public decimal Balance { get { return TotalPrice - TotalPaid; } }

        // This booking's individual ClientPayments rows, nested under it so each one can
        // link to its own printable receipt.
        public List<ClientPaymentRow> Payments { get; set; } = new List<ClientPaymentRow>();

        // This booking's customization selections with their review status, so Accounts can see what is (and
        // is not) part of the Total Price: only Approved ones are billed.
        public List<BookingSelectionRow> Selections { get; set; } = new List<BookingSelectionRow>();
    }

    // One customization selection under a booking, as Accounts sees it.
    public class BookingSelectionRow
    {
        public string Name { get; set; }
        public decimal Cost { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public string StatusLabel { get { return SelectionStatusLabel.For(Status, Remarks); } }
        public bool IsBilled { get { return Status == "Approved" && Cost > 0; } }
    }

    // One individual ClientPayments transaction, nested under its booking.
    public class ClientPaymentRow
    {
        public int PaymentID { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
    }

    // Shown on the printable Receipt page for a Client payment.
    public class PaymentReceiptModel
    {
        public int PaymentID { get; set; }
        public decimal Amount { get; set; }
        public DateTime PaymentDate { get; set; }
        public string PartyLabel { get; set; }
        public string PartyName { get; set; }
        public string ProjectName { get; set; }
        public string ExtraDetail { get; set; }

        // Overall total owed and remaining balance for the booking this
        // payment belongs to (as of all payments recorded so far), shown on the receipt
        // alongside the amount actually paid in this one transaction.
        public string TotalLabel { get; set; } = "Total Price";
        public decimal TotalPrice { get; set; }
        public decimal Due { get; set; }
    }

    // Per-project client payment total.
    public class ProjectLedgerModel
    {
        public int ProjectID { get; set; }
        public string ProjectName { get; set; }
        public decimal TotalClientPayments { get; set; }
    }
}
