using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.Geometry;
using Autodesk.AutoCAD.GraphicsInterface;
using Autodesk.AutoCAD.Runtime;
using Autodesk.Civil.ApplicationServices;
using Autodesk.Civil.DatabaseServices;
using System.Linq;

namespace ProjCOGOtoProf
{
    public class PViewCOGOPointCmd
    {
        private static PViewCOGOPointDO _overrule = null;

        [CommandMethod("OnOffPViewCogoPoint")]
        public void Run()
        {
            if (_overrule == null)
            {
                _overrule = new PViewCOGOPointDO();
                Overrule.AddOverrule
                    (RXClass.GetClass(typeof(CogoPoint)), _overrule, false);

            }
            else
            {
                Overrule.RemoveOverrule
                    (RXClass.GetClass(typeof(CogoPoint)), _overrule);
                _overrule = null;
            }
            Application.DocumentManager.
                MdiActiveDocument.Editor.Regen();
        }
    }

    public class PViewCOGOPointDO : DrawableOverrule
    {
        static bool NotExist(ObjectId id)
        {
            return id.IsNull
                || !id.IsValid
                || id.IsErased
                || id.IsEffectivelyErased;
        }

        public override bool WorldDraw(Drawable drawable, WorldDraw wd)
        {
            if (drawable is CogoPoint)
            {
                CogoPoint cogoPt = drawable as CogoPoint;

                Database db = HostApplicationServices.WorkingDatabase;
                CivilDocument cdoc = CivilDocument.GetCivilDocument(db);
                ObjectId[] alignIds = cdoc.GetAlignmentIds().Cast<ObjectId>().ToArray();

                // Проходим по трассам четрежа
                foreach (ObjectId alignId in alignIds)
                {
                    // Если трассы нет
                    if (NotExist(alignId))
                        // Переходим к следующей
                        continue;

                    // Пикетаж и смещение точки
                    double station = 0.0, offset = 0.0;

                    // Открываем трассу на чтение
#pragma warning disable 618
                    using (Alignment align = alignId.Open
                        (OpenMode.ForRead, false, true) as Alignment)
#pragma warning restore 618
                    {
                        align.StationOffset(cogoPt.Location.X, cogoPt.Location.Y, ref station, ref offset);

                        // Получаем виды профилей трассы
                        ObjectId[] alignPViewIds = align.GetProfileViewIds().Cast<ObjectId>().ToArray();

                        // проходим по видам профилей
                        foreach (ObjectId pViewId in alignPViewIds)
                        {
                            if (NotExist(pViewId))
                                continue;
#pragma warning disable 618
                            using (ProfileView pView = pViewId.Open
                                (OpenMode.ForRead, false, true) as ProfileView)
#pragma warning restore 618
                            {
                                double pViewCoordX = 0.0, pViewCoordY = 0.0;

                                // Если тока проецируется на вид профиля
                                if (pView.FindXYAtStationAndElevation(station, cogoPt.Location.Z, ref pViewCoordX, ref pViewCoordY))
                                {
                                    // Рисуем отображение окружности в этой точке
                                    // Здесь можно отобразить любой объект (кроме маскировки), в том числе и блок
                                    using (Circle ci = new Circle
                                        (new Point3d(pViewCoordX, pViewCoordY, pView.Location.Z),
                                        Vector3d.ZAxis, 2.0))
                                    {
                                        ci.WorldDraw(wd);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            return base.WorldDraw(drawable, wd);
        }
    }
}