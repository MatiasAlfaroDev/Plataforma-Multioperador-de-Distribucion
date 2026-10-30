using Android.App;
using Android.Runtime;

namespace UltimaMilla.Mobile
{
    [Application]
    public class MainApplication : MauiApplication
    {
        public MainApplication(IntPtr handle, JniHandleOwnership ownership)
            : base(handle, ownership)
        {
        }

        protected override MauiApp CreateMauiApp() => MauiProgram.CreateMauiApp();

        public override void OnCreate()
        {
            base.OnCreate();

            // Canal de los avisos push: tiene que existir antes de que llegue el primero,
            // aunque la app la abra una notificación con la app cerrada.
            CanalesDeAvisos.Crear(this);
        }
    }
}
