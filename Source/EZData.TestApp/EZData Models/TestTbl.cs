using System;

class TestTbl : EZData.DBTable
{
    [EZData.PrimaryKey]
	public Decimal Id { get; set; }
	public String LastName { get; set; }
	public String FirstName { get; set; }
	public String Address { get; set; }
	public DateTime PaymentDate { get; set; }
	public Decimal PaymentAmount { get; set; }
}