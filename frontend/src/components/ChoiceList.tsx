"use client";

import { useEffect, useId, useRef, useState } from "react";
import { createPortal } from "react-dom";

export type ChoiceOption = {
  value: string;
  label: string;
};

export function ChoiceList({
  label,
  options,
  value,
  onChange,
  error,
  hint,
  placeholder = "Choisir",
}: {
  label: string;
  options: ChoiceOption[];
  value: string | null;
  onChange: (value: string | null) => void;
  error?: string;
  hint?: string;
  placeholder?: string;
}) {
  const [open, setOpen] = useState(false);
  const [mounted, setMounted] = useState(false);
  const [menuBox, setMenuBox] = useState({ top: 0, left: 0, width: 0 });
  const rootRef = useRef<HTMLDivElement>(null);
  const buttonRef = useRef<HTMLButtonElement>(null);
  const listId = useId();
  const current = options.find((option) => option.value === value);

  useEffect(() => setMounted(true), []);

  useEffect(() => {
    if (!open) return;

    function place() {
      const rect = buttonRef.current?.getBoundingClientRect();
      if (!rect) return;
      const menuHeight = options.length * 44 + 12;
      const below = rect.bottom + 8;
      const openUp = below + menuHeight > window.innerHeight && rect.top > menuHeight;
      setMenuBox({
        top: openUp ? rect.top - menuHeight - 8 : below,
        left: rect.left,
        width: rect.width,
      });
    }

    function onPointer(event: MouseEvent) {
      const target = event.target as Node;
      if (rootRef.current?.contains(target)) return;
      if (document.getElementById(listId)?.contains(target)) return;
      setOpen(false);
    }

    function onKey(event: KeyboardEvent) {
      if (event.key === "Escape") setOpen(false);
    }

    place();
    window.addEventListener("mousedown", onPointer);
    window.addEventListener("keydown", onKey);
    window.addEventListener("resize", place);
    window.addEventListener("scroll", place, true);
    return () => {
      window.removeEventListener("mousedown", onPointer);
      window.removeEventListener("keydown", onKey);
      window.removeEventListener("resize", place);
      window.removeEventListener("scroll", place, true);
    };
  }, [open, listId, options.length]);

  return (
    <div ref={rootRef} className="auth-rise mt-4" style={{ animationDelay: "320ms" }}>
      <span id={`${listId}-label`} className="text-sm font-medium">
        {label}
      </span>
      {hint ? <p className="mt-1 text-xs text-neutral-500">{hint}</p> : null}
      <button
        ref={buttonRef}
        type="button"
        aria-haspopup="listbox"
        aria-expanded={open}
        aria-controls={listId}
        aria-labelledby={`${listId}-label`}
        onClick={() => setOpen((currentOpen) => !currentOpen)}
        className={`mt-2 flex w-full items-center justify-between rounded-xl border border-neutral-200 bg-white px-3.5 py-3 text-left text-base shadow-[0_8px_24px_rgba(0,0,0,0.06)] transition duration-300 ease-out outline-none hover:shadow-[0_10px_28px_rgba(0,0,0,0.1)] ${
          open ? "border-neutral-400 shadow-[0_12px_32px_rgba(0,0,0,0.12)] ring-4 ring-black/5" : ""
        }`}
      >
        <span className={current ? "text-black" : "text-neutral-400"}>{current?.label ?? placeholder}</span>
        <svg
          viewBox="0 0 16 16"
          className={`h-4 w-4 transition duration-300 ${open ? "rotate-180" : ""}`}
          aria-hidden
        >
          <path d="M4 6.2 8 10.2 12 6.2" fill="none" stroke="currentColor" strokeWidth="1.4" strokeLinecap="round" />
        </svg>
      </button>
      {error ? <p className="mt-1.5 text-xs">{error}</p> : null}
      {mounted && open
        ? createPortal(
            <ul
              id={listId}
              role="listbox"
              aria-labelledby={`${listId}-label`}
              className="choice-menu fixed z-30 overflow-hidden rounded-xl border border-neutral-200 bg-white py-1 shadow-[0_18px_50px_rgba(0,0,0,0.12)]"
              style={{ top: menuBox.top, left: menuBox.left, width: menuBox.width }}
            >
              {options.map((option) => {
                const selected = option.value === value;
                return (
                  <li key={option.value}>
                    <button
                      type="button"
                      role="option"
                      aria-selected={selected}
                      onClick={() => {
                        onChange(selected ? null : option.value);
                        setOpen(false);
                      }}
                      className={`flex w-full items-center justify-between px-3.5 py-2.5 text-left text-sm transition duration-200 ${
                        selected ? "bg-black text-white" : "text-black hover:bg-neutral-100"
                      }`}
                    >
                      <span>{option.label}</span>
                      <svg viewBox="0 0 12 12" className={`h-3 w-3 ${selected ? "opacity-100" : "opacity-0"}`} aria-hidden>
                        <path d="M2 6.2 4.6 9 10 3" fill="none" stroke="currentColor" strokeWidth="1.6" strokeLinecap="round" />
                      </svg>
                    </button>
                  </li>
                );
              })}
            </ul>,
            document.body,
          )
        : null}
    </div>
  );
}
