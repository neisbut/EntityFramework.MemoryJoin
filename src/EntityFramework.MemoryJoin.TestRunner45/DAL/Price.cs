using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EntityFramework.MemoryJoin.TestRunner45.DAL
{
    [Table("price", Schema = "public")]
    public class Price
    {
        [Column("ticker"), Key()]
        public string Ticker { get; set; }

        [Column("traded_on")]
        public DateTime TradedOn { get; set; }

        [Column("source_id")]
        public int PriceSourceId { get; set; }

    }
}
