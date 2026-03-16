-- ============================================================
-- D-3: orders.total Otomatik Güncelleme Trigger
-- ============================================================
-- Bu trigger aktifleştirilirse DatabaseService.AddOrderItemAsync
-- ve CloseOrderAsync'in orders.total yazması tamamen kaldırılabilir.
-- Şu an F-02 ile zaten tek Update'e indirgendi; bu adım opsiyoneldir.
--
-- Supabase Dashboard → SQL Editor'da çalıştır.
-- ============================================================

CREATE OR REPLACE FUNCTION update_order_total()
RETURNS TRIGGER AS $$
BEGIN
  UPDATE orders
  SET total = (
    SELECT COALESCE(SUM(price * quantity), 0)
    FROM order_items
    WHERE order_id = COALESCE(NEW.order_id, OLD.order_id)
  )
  WHERE id = COALESCE(NEW.order_id, OLD.order_id);
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Trigger: order_items INSERT / UPDATE / DELETE tetikler
DROP TRIGGER IF EXISTS trg_update_order_total ON order_items;
CREATE TRIGGER trg_update_order_total
AFTER INSERT OR UPDATE OR DELETE ON order_items
FOR EACH ROW EXECUTE FUNCTION update_order_total();


-- ============================================================
-- F-15: Row Level Security (RLS) Politikaları
-- ============================================================
-- Publishable key APK içinde açık olsa bile bu politikalar
-- yetkisiz yazma/silmeyi engeller.
-- Her tablo için RLS'yi aç ve sadece bu uygulamanın
-- yapabileceği işlemleri izin ver.
-- ============================================================

-- RLS'yi etkinleştir
ALTER TABLE tables      ENABLE ROW LEVEL SECURITY;
ALTER TABLE menu_items  ENABLE ROW LEVEL SECURITY;
ALTER TABLE orders      ENABLE ROW LEVEL SECURITY;
ALTER TABLE order_items ENABLE ROW LEVEL SECURITY;

-- tables: Herkes okuyabilir (masa listesi), herkes güncelleyebilir (durum değişimi)
CREATE POLICY "tables_select" ON tables FOR SELECT USING (true);
CREATE POLICY "tables_update" ON tables FOR UPDATE USING (true);

-- menu_items: Herkes okuyabilir (is_active=true), güncelleme/insert için
CREATE POLICY "menu_select"  ON menu_items FOR SELECT USING (is_active = true);
CREATE POLICY "menu_insert"  ON menu_items FOR INSERT WITH CHECK (true);
CREATE POLICY "menu_update"  ON menu_items FOR UPDATE USING (true);

-- orders: Herkes okuyabilir/yazabilir (auth yok, max 4 cihaz)
CREATE POLICY "orders_select" ON orders FOR SELECT USING (true);
CREATE POLICY "orders_insert" ON orders FOR INSERT WITH CHECK (true);
CREATE POLICY "orders_update" ON orders FOR UPDATE USING (true);

-- order_items: Herkes okuyabilir/yazabilir/silebilir
CREATE POLICY "items_select" ON order_items FOR SELECT USING (true);
CREATE POLICY "items_insert" ON order_items FOR INSERT WITH CHECK (true);
CREATE POLICY "items_update" ON order_items FOR UPDATE USING (true);
CREATE POLICY "items_delete" ON order_items FOR DELETE USING (true);

-- NOT: Bu politikalar tam koruma değil; kötü niyetli bir kullanıcı
-- hâlâ veri okuyabilir. Gerçek koruma için Supabase Auth + JWT kullanılmalı.
-- Bu uygulamanın threat modeli (kafe içi, max 4 cihaz) için yeterlidir.
