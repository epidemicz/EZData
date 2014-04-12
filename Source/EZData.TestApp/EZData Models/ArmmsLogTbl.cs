using System;

class ArmmsLogTbl : EZData.DBTable
{
	public String LogType { get; set; }
	public String AppName { get; set; }
	public DateTime DateSys { get; set; }
	public DateTime DateWkstation { get; set; }
	public String ProcessName { get; set; }
	public String ProcessAction { get; set; }
	public String ProcessLoc { get; set; }
	public String ProcessInfo { get; set; }
	public String ProcessExtInfo { get; set; }
	public String CurUser { get; set; }
	public Decimal CurSqlcode { get; set; }
	public String CurSqlerrm { get; set; }
	public String CurItem { get; set; }
	public String CurRec { get; set; }
	public String CurVal { get; set; }
	public String CurMouseItem { get; set; }
	public String CurApp { get; set; }
	public String MsgToUser { get; set; }
	public String WkstationName { get; set; }
	public Decimal PrimKeyNum { get; set; }
	public String PrimKeyName { get; set; }
	public String SubjectName { get; set; }
	public Decimal Atn { get; set; }
	public String ItemNum { get; set; }
	public String Afis { get; set; }
	public Decimal Nameid { get; set; }
}