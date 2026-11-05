import { hu } from "../i18n/hu";

export default function PrivacyPage() {
  return (
    <main className="mx-auto max-w-3xl space-y-4 px-4 py-12 prose prose-stone">
      <h1 className="font-[Fraunces] text-3xl">Adatvédelmi tájékoztató</h1>
      <p>{hu.privacy}</p>
      <p>
        A megrendelés során gyűjtött adatok: név, telefonszám, opcionális e-mail, családi fényképek, személyes
        események. Az adatokat a naptár előállításához és ügyfélszolgálathoz használjuk.
      </p>
      <p>
        A szerkesztő link birtokában a megrendelés módosítható; kérje az adminisztrátortól a link visszavonását, ha
        az kompromittálódott.
      </p>
      <p>
        Ez a tájékoztató technikai segítséget nyújt, de önmagában nem helyettesíti a jogi GDPR megfelelőségi
        értékelést. A jogi és üzemeltetési felelősség a szolgáltatóé marad.
      </p>
    </main>
  );
}
