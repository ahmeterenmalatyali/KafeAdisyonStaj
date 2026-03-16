using FluentAssertions;
using KafeAdisyon.Models;
using KafeAdisyon.Tests.TestInfrastructure;
using Xunit;

namespace KafeAdisyon.Tests
{
    /// <summary>
    /// SplitBillPage + OrderPage hesap bölme mantığının tüm senaryoları.
    /// Her test: hangi durumu test ettiğini, beklenen davranışı ve neden önemli olduğunu açıklar.
    /// </summary>
    public class SplitBillCalculatorTests
    {
        // ─── TEMEL HESAPLAMA ──────────────────────────────────────────────

        [Fact(DisplayName = "Seçilen ürün toplamı: fiyat × seçilen adet")]
        public void SelectedTotal_SingleItem_CorrectAmount()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 3)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2);

            calc.GetSelectedTotal().Should().Be(100); // 50 × 2
        }

        [Fact(DisplayName = "Hiç seçim yapılmamışsa seçilen toplam sıfır olur")]
        public void SelectedTotal_NoSelection_ReturnsZero()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 100, qty: 2),
                TestFactory.OrderItem("i2", "m2", price: 50, qty: 1)
            };
            var calc = new SplitBillCalculator(items);

            calc.GetSelectedTotal().Should().Be(0);
        }

        [Fact(DisplayName = "Tamamı seçilince seçilen toplam = sipariş toplamı")]
        public void SelectedTotal_AllSelected_EqualsOrderTotal()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 80, qty: 2),
                TestFactory.OrderItem("i2", "m2", price: 30, qty: 3)
            };
            var calc = new SplitBillCalculator(items);
            calc.SelectAll();

            calc.GetSelectedTotal().Should().Be(calc.GetOrderTotal()); // 160 + 90 = 250
        }

        [Fact(DisplayName = "Seçilen + kalan = toplam sipariş (tutarlılık garantisi)")]
        public void SelectedPlusRemaining_AlwaysEqualsOrderTotal()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 45, qty: 4),
                TestFactory.OrderItem("i2", "m2", price: 120, qty: 1),
                TestFactory.OrderItem("i3", "m3", price: 25, qty: 3)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2);
            calc.SetSelection("i3", 1);

            var orderTotal = calc.GetOrderTotal();
            var selected = calc.GetSelectedTotal();
            var remaining = calc.GetRemainingTotal();

            (selected + remaining).Should().BeApproximately(orderTotal, 0.001,
                "seçilen + kalan her zaman sipariş toplamına eşit olmalı");
        }

        // ─── ÇOKLU ÜRÜN SENARYOLARI ───────────────────────────────────────

        [Fact(DisplayName = "Çoklu ürün: karışık seçimde doğru toplam")]
        public void MultipleItems_MixedSelection_CorrectTotals()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 20, qty: 3),  // 60 TL toplam
                TestFactory.OrderItem("i2", "m2", price: 50, qty: 2),  // 100 TL toplam
                TestFactory.OrderItem("i3", "m3", price: 15, qty: 4)   // 60 TL toplam
            };
            // Toplam: 220 TL
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2);  // 40 TL seçildi
            calc.SetSelection("i2", 1);  // 50 TL seçildi
            // i3 seçilmedi

            calc.GetSelectedTotal().Should().Be(90);       // 40 + 50
            calc.GetRemainingTotal().Should().Be(130);     // 20 + 50 + 60
            calc.GetOrderTotal().Should().Be(220);
        }

        [Fact(DisplayName = "Tek birimlik ürün tam seçilince kalan sıfır olur")]
        public void SingleUnitItem_FullySelected_RemainingZero()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 75, qty: 1)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 1);

            calc.GetRemainingTotal().Should().Be(0);
            calc.GetSelectedTotal().Should().Be(75);
        }

        // ─── ComputeChanges (toRemove / toUpdate) ────────────────────────

        [Fact(DisplayName = "Tüm adetleri seçilen ürün toRemove listesine girer")]
        public void ComputeChanges_AllQtySelected_GoesToRemove()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 40, qty: 2)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2); // tamamı seçildi

            var (toRemove, toUpdate) = calc.ComputeChanges();

            toRemove.Should().ContainSingle(i => i.Id == "i1");
            toUpdate.Should().BeEmpty();
        }

        [Fact(DisplayName = "Kısmi seçilen ürün toUpdate listesine girer, kalan doğru")]
        public void ComputeChanges_PartialQtySelected_GoesToUpdate()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 30, qty: 3)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 1); // 1 tanesi ödeniyor, 2 kalıyor

            var (toRemove, toUpdate) = calc.ComputeChanges();

            toRemove.Should().BeEmpty();
            toUpdate.Should().ContainSingle();
            toUpdate[0].Remaining.Should().Be(2);
        }

        [Fact(DisplayName = "Seçilmeyen ürün ne toRemove ne toUpdate'e girer")]
        public void ComputeChanges_UnselectedItem_NotInEitherList()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2),
                TestFactory.OrderItem("i2", "m2", price: 30, qty: 1)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2); // sadece i1 seçildi

            var (toRemove, toUpdate) = calc.ComputeChanges();

            toRemove.Select(i => i.Id).Should().NotContain("i2");
            toUpdate.Select(t => t.Item.Id).Should().NotContain("i2");
        }

        // ─── PostSplitTotal (kalan sipariş) ──────────────────────────────

        [Fact(DisplayName = "Split sonrası kalan toplam: sadece kalan ürünlerin toplamı")]
        public void PostSplitTotal_PartialPayment_CorrectRemaining()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2), // 100 TL
                TestFactory.OrderItem("i2", "m2", price: 30, qty: 2)  // 60 TL
            };
            // Toplam 160 TL
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2); // 100 TL ödendi

            calc.ComputePostSplitTotal().Should().Be(60); // sadece i2 kaldı
        }

        [Fact(DisplayName = "Split sonrası kalan: kısmi adet seçilince kalan adet × fiyat")]
        public void PostSplitTotal_PartialQty_CorrectRemainingQtyTimesPrice()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 25, qty: 4) // 100 TL
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 1); // 25 TL ödendi, 3 adet kaldı

            calc.ComputePostSplitTotal().Should().Be(75); // 25 × 3
        }

        [Fact(DisplayName = "Tümü ödenince kalan toplam sıfır")]
        public void PostSplitTotal_AllPaid_Zero()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 40, qty: 3),
                TestFactory.OrderItem("i2", "m2", price: 60, qty: 1)
            };
            var calc = new SplitBillCalculator(items);
            calc.SelectAll();

            calc.ComputePostSplitTotal().Should().Be(0);
        }

        // ─── UÇ DURUMLAR ──────────────────────────────────────────────────

        [Fact(DisplayName = "UÇ: Tek ürünlü, 1 adetli sipariş — tam ödeme")]
        public void EdgeCase_SingleItemSingleQty_FullPayment()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 100, qty: 1)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 1);

            calc.GetSelectedTotal().Should().Be(100);
            calc.GetRemainingTotal().Should().Be(0);
            calc.ComputePostSplitTotal().Should().Be(0);
        }

        [Fact(DisplayName = "UÇ: Çok sayıda ürün — toplam tutarlılığı")]
        public void EdgeCase_ManyItems_TotalConsistency()
        {
            var rnd = new Random(42);
            var items = Enumerable.Range(1, 20).Select(i =>
                TestFactory.OrderItem($"i{i}", $"m{i}",
                    price: rnd.Next(10, 200),
                    qty: rnd.Next(1, 5))
            ).ToList();

            var calc = new SplitBillCalculator(items);
            // Rastgele bir kısmını seç
            foreach (var item in items.Where(i => i.Id.GetHashCode() % 2 == 0))
                calc.SetSelection(item.Id, item.Quantity);

            var orderTotal = calc.GetOrderTotal();
            var selected = calc.GetSelectedTotal();
            var remaining = calc.GetRemainingTotal();

            (selected + remaining).Should().BeApproximately(orderTotal, 0.001,
                "20 ürünlü siparişte de seçilen + kalan = toplam olmalı");
        }

        [Fact(DisplayName = "UÇ: Aynı fiyatlı birden fazla ürün — ayrı ayrı takip edilir")]
        public void EdgeCase_SamePriceItems_TrackedSeparately()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2),
                TestFactory.OrderItem("i2", "m2", price: 50, qty: 2) // aynı fiyat, farklı ürün
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2); // sadece i1 ödeniyor

            calc.GetSelectedTotal().Should().Be(100); // sadece i1
            calc.GetRemainingTotal().Should().Be(100); // sadece i2
            calc.ComputePostSplitTotal().Should().Be(100); // i2 kaldı
        }

        [Fact(DisplayName = "UÇ: Geçersiz seçim (adet > stok) reddedilir")]
        public void EdgeCase_InvalidSelection_Rejected()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2)
            };
            var calc = new SplitBillCalculator(items);
            var result = calc.SetSelection("i1", 3); // max 2, 3 isteği reddedilmeli

            result.Should().BeFalse();
            calc.GetSelection("i1").Should().Be(0); // değişmemiş olmalı
        }

        [Fact(DisplayName = "UÇ: Negatif seçim reddedilir")]
        public void EdgeCase_NegativeSelection_Rejected()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2)
            };
            var calc = new SplitBillCalculator(items);
            var result = calc.SetSelection("i1", -1);

            result.Should().BeFalse();
        }

        [Fact(DisplayName = "UÇ: Var olmayan item ID ile seçim reddedilir")]
        public void EdgeCase_NonExistentItemId_Rejected()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2)
            };
            var calc = new SplitBillCalculator(items);
            var result = calc.SetSelection("YOK_ID", 1);

            result.Should().BeFalse();
        }

        [Fact(DisplayName = "UÇ: Sıfır seçim ile önceki seçimi sıfırlamak mümkün")]
        public void EdgeCase_ResetSelectionToZero_Works()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 3)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 2);
            calc.SetSelection("i1", 0); // sıfırla

            calc.GetSelectedTotal().Should().Be(0);
            calc.GetSelection("i1").Should().Be(0);
        }

        [Fact(DisplayName = "UÇ: SelectNone sonrası tüm seçimler sıfırlanır")]
        public void EdgeCase_SelectNone_ClearsAllSelections()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2),
                TestFactory.OrderItem("i2", "m2", price: 30, qty: 3)
            };
            var calc = new SplitBillCalculator(items);
            calc.SelectAll();
            calc.SelectNone();

            calc.GetSelectedTotal().Should().Be(0);
        }

        [Fact(DisplayName = "UÇ: Çok düşük fiyat (1 kuruş) hesaplama hatası yok")]
        public void EdgeCase_VeryLowPrice_NoRoundingError()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 0.01, qty: 100) // 1 TL toplam
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 50);

            calc.GetSelectedTotal().Should().BeApproximately(0.50, 0.001);
            calc.GetRemainingTotal().Should().BeApproximately(0.50, 0.001);
        }

        [Fact(DisplayName = "UÇ: Çok yüksek fiyat ve adet (stres testi)")]
        public void EdgeCase_HighPriceAndQty_CorrectTotal()
        {
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 9999, qty: 999)
            };
            var calc = new SplitBillCalculator(items);
            calc.SetSelection("i1", 500);

            calc.GetSelectedTotal().Should().BeApproximately(4_999_500, 1);
            calc.GetRemainingTotal().Should().BeApproximately(4_989_501, 1);
        }

        [Fact(DisplayName = "Arka arkaya iki split işlemi — toplam tutarlılığı")]
        public void TwoConsecutiveSplits_TotalConsistency()
        {
            // Simüle: 3 ürün, önce 1 split, sonra kalan üzerinde tekrar split
            var items = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 4), // 200
                TestFactory.OrderItem("i2", "m2", price: 80, qty: 2), // 160
                TestFactory.OrderItem("i3", "m3", price: 30, qty: 3)  // 90
            };
            // Toplam 450 TL
            var calc = new SplitBillCalculator(items);

            // 1. split: i1'den 2 adet ödeniyor
            calc.SetSelection("i1", 2);
            double firstSplit = calc.GetSelectedTotal(); // 100
            double afterFirst = calc.ComputePostSplitTotal(); // 350

            firstSplit.Should().Be(100);
            afterFirst.Should().Be(350);
            (firstSplit + afterFirst).Should().BeApproximately(450, 0.001);

            // 2. split üzerinde: kalan i1 (2 adet) + i2 + i3
            var remainingItems = new List<OrderItemModel>
            {
                TestFactory.OrderItem("i1", "m1", price: 50, qty: 2),
                TestFactory.OrderItem("i2", "m2", price: 80, qty: 2),
                TestFactory.OrderItem("i3", "m3", price: 30, qty: 3)
            };
            var calc2 = new SplitBillCalculator(remainingItems);
            calc2.SetSelection("i2", 2); // i2 tamamen ödeniyor
            double secondSplit = calc2.GetSelectedTotal(); // 160
            double afterSecond = calc2.ComputePostSplitTotal(); // 190

            secondSplit.Should().Be(160);
            afterSecond.Should().Be(190);
            (firstSplit + secondSplit + afterSecond).Should().BeApproximately(450, 0.001,
                "toplam her adımda korunmalı");
        }
    }
}
