// A capital I lowercases to a dotless i in Turkish, which an English keyboard's "i" would never match.
export const foldSearch = (text: string) => text.toLocaleLowerCase("tr-TR").replaceAll("ı", "i");
