import { useEffect, useState } from "react";
import { api } from "@/lib/api";

// Loads an image from an authenticated API endpoint. A plain <img src> can't
// send the JWT, so we fetch the bytes via the api client and show an object URL.
export default function AuthImage({ src, alt = "", className = "" }: { src: string; alt?: string; className?: string }) {
  const [url, setUrl] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    let objectUrl: string | null = null;
    api
      .get(src, { responseType: "blob" })
      .then((res) => {
        if (!active) return;
        objectUrl = URL.createObjectURL(res.data as Blob);
        setUrl(objectUrl);
      })
      .catch(() => {});
    return () => {
      active = false;
      if (objectUrl) URL.revokeObjectURL(objectUrl);
    };
  }, [src]);

  if (!url) return <div className={`${className} bg-slate-100 animate-pulse`} aria-hidden />;
  return <img src={url} alt={alt} className={className} />;
}
