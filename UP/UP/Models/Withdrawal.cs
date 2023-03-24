﻿namespace UP.Models
{
    public class Withdrawal
    {
        public int Id { get; set; }
        public DateTime Date { get; set; }
        public double Quantity { get; set; }
        public double Commission { get; set; }
        public int UserId { get; set; }

        public Withdrawal(int id, DateTime date, double quantity, double commission, int userId)
        {
            Id = id;
            Date = date;
            Quantity = quantity;
            Commission = commission;
            UserId = userId;
        }

        public Withdrawal()
        {
        }
    }
}